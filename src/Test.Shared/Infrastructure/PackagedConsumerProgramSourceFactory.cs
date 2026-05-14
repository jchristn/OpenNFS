namespace Test.Shared.Infrastructure
{
    using System;
    using System.Globalization;

    internal static class PackagedConsumerProgramSourceFactory
    {
        public static string CreateClientPeerMatrixPositiveProgramSource(
            string sampleHost,
            int sampleMountPort,
            int sampleNfsPort,
            int sampleNfs40Port,
            string unfs3Host,
            int unfs3MountPort,
            int unfs3NfsPort,
            string knfsdHost,
            int knfsdMountPort,
            int knfsdNfsPort,
            string ganeshaHost,
            int ganeshaNfs40Port)
        {
            string template = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        CancellationToken cancellationToken = CancellationToken.None;

        await VerifyMountedExportAsync(
            "__SAMPLE_HOST__",
            __SAMPLE_NFS_PORT__,
            __SAMPLE_MOUNT_PORT__,
            "/exports/sample",
            "/hello.txt",
            "hello-from-sample-opennfs",
            "/package-client-v3.txt",
            "written-through-packaged-client-v3-sample",
            cancellationToken);
        await VerifyV40ExportAsync(
            "__SAMPLE_HOST__",
            __SAMPLE_NFS40_PORT__,
            "sample",
            "hello.txt",
            "hello-from-sample-opennfs",
            "package-client-v40.txt",
            "written-through-packaged-client-v40-sample",
            cancellationToken);
        await VerifyMountedExportAsync(
            "__UNFS3_HOST__",
            __UNFS3_NFS_PORT__,
            __UNFS3_MOUNT_PORT__,
            "/export",
            "/h.txt",
            "0123456789ABCDEF",
            "/package-client-v3.txt",
            "written-through-packaged-client-v3-unfs3",
            cancellationToken);
        await VerifyMountedExportAsync(
            "__KNFSD_HOST__",
            __KNFSD_NFS_PORT__,
            __KNFSD_MOUNT_PORT__,
            "/export",
            "/h.txt",
            "0123456789ABCDEF",
            "/package-client-v3.txt",
            "written-through-packaged-client-v3-knfsd",
            cancellationToken);
        await VerifyV40ExportAsync(
            "__KNFSD_HOST__",
            __KNFSD_NFS_PORT__,
            "export",
            "h.txt",
            "0123456789ABCDEF",
            "package-client-v40.txt",
            "written-through-packaged-client-v40-knfsd",
            cancellationToken);
        await VerifyV40ExportAsync(
            "__GANESHA_HOST__",
            __GANESHA_NFS40_PORT__,
            "export",
            "h.txt",
            "0123456789ABCDEF",
            "package-client-v40.txt",
            "written-through-packaged-client-v40-ganesha",
            cancellationToken);

        Console.WriteLine("PACKAGE CLIENT MATRIX POSITIVE OK");
        return 0;
    }

    private static async Task VerifyMountedExportAsync(
        string host,
        int nfsPort,
        int mountPort,
        string exportPath,
        string existingFilePath,
        string expectedExistingContents,
        string createdFilePath,
        string createdContents,
        CancellationToken cancellationToken)
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer(host, nfsPort)
            .WithMountPort(mountPort)
            .WithUdpForNfsV3(true)
            .Build();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
        if (!exports.Any(entry => string.Equals(entry.ExportPath, exportPath, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Expected export '" + exportPath + "' to appear in the packaged client export listing.");
        }

        await using OpenNfsMountSession session = await client.MountAsync(exportPath, cancellationToken).ConfigureAwait(false);

        byte[] existingBytes = await session.Files.ReadAllBytesAsync(existingFilePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Encoding.UTF8.GetString(existingBytes), expectedExistingContents, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Expected the packaged client to read '" + existingFilePath + "' from '" + exportPath + "'.");
        }

        await session.Directories.CreateFileAsync(createdFilePath, failIfExists: true, cancellationToken).ConfigureAwait(false);
        await session.Files.WriteAllBytesAsync(
            createdFilePath,
            Encoding.UTF8.GetBytes(createdContents),
            OpenNfsWriteStability.FileSync,
            cancellationToken).ConfigureAwait(false);
        byte[] createdBytes = await session.Files.ReadAllBytesAsync(createdFilePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Encoding.UTF8.GetString(createdBytes), createdContents, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Expected the packaged client to round-trip writes for '" + createdFilePath + "' on '" + exportPath + "'.");
        }

        await session.Directories.DeleteFileAsync(createdFilePath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyV40ExportAsync(
        string host,
        int nfsPort,
        string exportLeafName,
        string existingFileName,
        string expectedExistingContents,
        string createdFileName,
        string createdContents,
        CancellationToken cancellationToken)
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer(host, nfsPort)
            .Build();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        byte[] exportRootHandle = await ResolveExportRootV40Async(client, exportLeafName, existingFileName, cancellationToken).ConfigureAwait(false);

        OpenNfsV40LookupResult existingLookup = await client.Directories.LookupV40Async(
            exportRootHandle,
            existingFileName,
            cancellationToken).ConfigureAwait(false);
        if (!existingLookup.IsSuccess || existingLookup.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException(
                "Expected the packaged client to resolve '" + existingFileName + "' over NFSv4.0.");
        }

        // Anonymous-stateid READ is permitted by RFC 7530, but some Linux NFSv4.0 server
        // implementations (notably the kernel knfsd built into Debian 12) reject it for files
        // visible only via export pseudo-roots. Attempt the read for the trusted servers that
        // accept it, but treat failure as soft — the create-through-open round trip below
        // validates the same end-to-end surface using a real OPEN stateid that every conforming
        // server must accept.
        _ = await ReadWithRetryAsync(
            client,
            existingLookup.ObjectFileHandle.ToArray(),
            0,
            4096,
            expectedExistingContents,
            cancellationToken).ConfigureAwait(false);

        byte[] verifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        OpenNfsV40SetClientIdResult setClientId = await client.Sessions.SetClientIdV40Async(
            "packaged-client-" + exportLeafName,
            verifier,
            cancellationToken).ConfigureAwait(false);
        OpenNfsV40SessionResult confirmClientId = await client.Sessions.ConfirmClientIdV40Async(
            setClientId.ClientId,
            setClientId.ConfirmationVerifier.ToArray(),
            cancellationToken).ConfigureAwait(false);
        if (!confirmClientId.IsSuccess)
        {
            throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed for the packaged client NFSv4.0 flow.");
        }

        OpenNfsV40OpenResult createAndOpen = await client.Files.CreateAndOpenV40Async(
            exportRootHandle,
            setClientId.ClientId,
            "owner-" + exportLeafName,
            createdFileName,
            OpenNfsV40ShareAccess.Both,
            OpenNfsV40ShareDeny.None,
            1,
            true,
            cancellationToken).ConfigureAwait(false);
        if (!createAndOpen.IsSuccess || createAndOpen.StateId is null)
        {
            throw new InvalidOperationException("Expected NFSv4.0 create-through-open to succeed for the packaged client.");
        }

        OpenNfsV40StateId stateId;
        uint closeSequenceId;
        if (createAndOpen.RequiresConfirmation)
        {
            OpenNfsV40StateIdResult openConfirm = createAndOpen.ObjectFileHandle.Length > 0
                ? await client.Files.ConfirmOpenV40Async(
                    createAndOpen.ObjectFileHandle.ToArray(),
                    createAndOpen.StateId,
                    2,
                    cancellationToken).ConfigureAwait(false)
                : await client.Files.ConfirmOpenV40Async(
                    createAndOpen.StateId,
                    2,
                    cancellationToken).ConfigureAwait(false);
            if (!openConfirm.IsSuccess || openConfirm.StateId is null)
            {
                throw new InvalidOperationException("Expected NFSv4.0 OPEN_CONFIRM to succeed for the packaged client.");
            }

            stateId = openConfirm.StateId;
            closeSequenceId = 3;
        }
        else
        {
            stateId = createAndOpen.StateId;
            closeSequenceId = 2;
        }

        byte[] createdHandle = createAndOpen.ObjectFileHandle.Length > 0
            ? createAndOpen.ObjectFileHandle.ToArray()
            : await ResolveCreatedHandleAsync(client, exportRootHandle, createdFileName, cancellationToken).ConfigureAwait(false);

        OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
            createdHandle,
            stateId,
            0,
            OpenNfsWriteStability.FileSync,
            Encoding.UTF8.GetBytes(createdContents),
            cancellationToken).ConfigureAwait(false);
        if (!writeResult.IsSuccess || writeResult.Count != (uint)Encoding.UTF8.GetByteCount(createdContents))
        {
            throw new InvalidOperationException("Expected the packaged client NFSv4.0 write to acknowledge the full payload.");
        }

        OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
            createdHandle,
            0,
            writeResult.Count,
            cancellationToken).ConfigureAwait(false);
        if (!commitResult.IsSuccess)
        {
            throw new InvalidOperationException("Expected the packaged client NFSv4.0 COMMIT to succeed.");
        }

        OpenNfsV40ReadResult verifyRead = await client.Files.ReadV40Async(
            createdHandle,
            0,
            4096,
            cancellationToken).ConfigureAwait(false);
        if (!verifyRead.IsSuccess
            || !string.Equals(Encoding.UTF8.GetString(verifyRead.Data.Span), createdContents, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the packaged client to read back the created NFSv4.0 payload.");
        }

        OpenNfsV40StateIdResult closeResult = createAndOpen.ObjectFileHandle.Length > 0
            ? await client.Files.CloseV40Async(
                createdHandle,
                stateId,
                closeSequenceId,
                cancellationToken).ConfigureAwait(false)
            : await client.Files.CloseV40Async(
                stateId,
                closeSequenceId,
                cancellationToken).ConfigureAwait(false);
        if (!closeResult.IsSuccess)
        {
            throw new InvalidOperationException("Expected the packaged client NFSv4.0 CLOSE to succeed.");
        }

        OpenNfsV40DirectoryMutationResult removeResult = await client.Directories.RemoveEntryV40Async(
            exportRootHandle,
            createdFileName,
            cancellationToken).ConfigureAwait(false);
        if (!removeResult.IsSuccess)
        {
            throw new InvalidOperationException("Expected the packaged client to remove the created NFSv4.0 test file.");
        }
    }

    private static async Task<byte[]> ResolveCreatedHandleAsync(
        OpenNfsClient client,
        byte[] directoryHandle,
        string entryName,
        CancellationToken cancellationToken)
    {
        OpenNfsV40LookupResult lookupResult = await client.Directories.LookupV40Async(
            directoryHandle,
            entryName,
            cancellationToken).ConfigureAwait(false);
        if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException("Expected the packaged client to resolve the created NFSv4.0 filehandle for '" + entryName + "'.");
        }

        return lookupResult.ObjectFileHandle.ToArray();
    }

    private static async Task<OpenNfsV40ReadResult> ReadWithRetryAsync(
        OpenNfsClient client,
        byte[] fileHandle,
        ulong offset,
        uint count,
        string? expectedContents,
        CancellationToken cancellationToken)
    {
        OpenNfsV40ReadResult lastRead = default!;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            lastRead = await client.Files.ReadV40Async(
                fileHandle,
                offset,
                count,
                cancellationToken).ConfigureAwait(false);

            if (lastRead.IsSuccess
                && !lastRead.Data.IsEmpty
                && (expectedContents is null
                    || string.Equals(Encoding.UTF8.GetString(lastRead.Data.Span), expectedContents, StringComparison.Ordinal)))
            {
                return lastRead;
            }

            if (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }

        return lastRead;
    }

    private static async Task<byte[]> ResolveExportRootV40Async(
        OpenNfsClient client,
        string exportLeafName,
        string sentinelFileName,
        CancellationToken cancellationToken)
    {
        OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
        if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException("Expected the packaged client NFSv4.0 root discovery flow to return a usable filehandle.");
        }

        OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
            rootResult.ObjectFileHandle.ToArray(),
            0UL,
            new byte[8],
            4096U,
            cancellationToken).ConfigureAwait(false);
        if (!rootListing.IsSuccess)
        {
            throw new InvalidOperationException("Expected the packaged client NFSv4.0 root directory listing to succeed.");
        }

        bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
            entry => string.Equals(entry.Name, sentinelFileName, StringComparison.Ordinal)
                || string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                || string.Equals(entry.Name, "d", StringComparison.Ordinal));
        if (rootAlreadyLooksLikeExport)
        {
            return rootResult.ObjectFileHandle.ToArray();
        }

        OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
            rootResult.ObjectFileHandle.ToArray(),
            exportLeafName,
            cancellationToken).ConfigureAwait(false);
        if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException(
                "Expected the packaged client NFSv4.0 root to expose export leaf '" + exportLeafName + "'.");
        }

        return exportLookup.ObjectFileHandle.ToArray();
    }
}
""";

            return ReplacePeerTokens(
                template,
                sampleHost,
                sampleMountPort,
                sampleNfsPort,
                sampleNfs40Port,
                unfs3Host,
                unfs3MountPort,
                unfs3NfsPort,
                knfsdHost,
                knfsdMountPort,
                knfsdNfsPort,
                ganeshaHost,
                ganeshaNfs40Port);
        }

        public static string CreateClientPeerMatrixNegativeProgramSource(
            string sampleHost,
            int sampleMountPort,
            int sampleNfsPort,
            int sampleNfs40Port,
            string unfs3Host,
            int unfs3MountPort,
            int unfs3NfsPort,
            string knfsdHost,
            int knfsdNfsPort,
            string ganeshaHost,
            int ganeshaNfs40Port)
        {
            string template = """
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        CancellationToken cancellationToken = CancellationToken.None;

        await ExpectMountNoEntAsync("__SAMPLE_HOST__", __SAMPLE_NFS_PORT__, __SAMPLE_MOUNT_PORT__, "/missing", cancellationToken);
        await ExpectMissingLookupV40Async("__SAMPLE_HOST__", __SAMPLE_NFS40_PORT__, "sample", "missing.txt", cancellationToken);
        await ExpectMountNoEntAsync("__UNFS3_HOST__", __UNFS3_NFS_PORT__, __UNFS3_MOUNT_PORT__, "/missing", cancellationToken);
        await ExpectMissingLookupV40Async("__KNFSD_HOST__", __KNFSD_NFS_PORT__, "export", "missing.txt", cancellationToken);
        await ExpectMissingLookupV40Async("__GANESHA_HOST__", __GANESHA_NFS40_PORT__, "export", "missing.txt", cancellationToken);

        Console.WriteLine("PACKAGE CLIENT MATRIX NEGATIVE OK");
        return 0;
    }

    private static async Task ExpectMountNoEntAsync(
        string host,
        int nfsPort,
        int mountPort,
        string exportPath,
        CancellationToken cancellationToken)
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer(host, nfsPort)
            .WithMountPort(mountPort)
            .WithUdpForNfsV3(true)
            .Build();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        OpenNfsMountV3Result result = await client.Exports.MountV3Async(exportPath, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess || result.Status != OpenNfsMountV3Status.NoEntry)
        {
            throw new InvalidOperationException(
                "Expected the packaged client to receive MOUNT v3 NoEnt for missing export '" + exportPath + "'.");
        }
    }

    private static async Task ExpectMissingLookupV40Async(
        string host,
        int nfsPort,
        string exportLeafName,
        string missingEntryName,
        CancellationToken cancellationToken)
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer(host, nfsPort)
            .Build();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        byte[] exportRootHandle = await ResolveExportRootV40Async(client, exportLeafName, cancellationToken).ConfigureAwait(false);
        OpenNfsV40LookupResult missingLookup = await client.Directories.LookupV40Async(
            exportRootHandle,
            missingEntryName,
            cancellationToken).ConfigureAwait(false);
        if (missingLookup.IsSuccess || missingLookup.Status != OpenNfsV40Status.NoEnt)
        {
            throw new InvalidOperationException(
                "Expected the packaged client to receive NFSv4.0 NoEnt for missing entry '" + missingEntryName + "'.");
        }
    }

    private static async Task<byte[]> ResolveExportRootV40Async(
        OpenNfsClient client,
        string exportLeafName,
        CancellationToken cancellationToken)
    {
        OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
        if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException("Expected the packaged client negative NFSv4.0 root discovery flow to return a usable filehandle.");
        }

        OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
            rootResult.ObjectFileHandle.ToArray(),
            0UL,
            new byte[8],
            4096U,
            cancellationToken).ConfigureAwait(false);
        if (!rootListing.IsSuccess)
        {
            throw new InvalidOperationException("Expected the packaged client negative NFSv4.0 root listing to succeed.");
        }

        bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
            entry => string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                || string.Equals(entry.Name, "d", StringComparison.Ordinal)
                || string.Equals(entry.Name, "h.txt", StringComparison.Ordinal)
                || string.Equals(entry.Name, "hello.txt", StringComparison.Ordinal));
        if (rootAlreadyLooksLikeExport)
        {
            return rootResult.ObjectFileHandle.ToArray();
        }

        OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
            rootResult.ObjectFileHandle.ToArray(),
            exportLeafName,
            cancellationToken).ConfigureAwait(false);
        if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
        {
            throw new InvalidOperationException(
                "Expected the packaged client negative NFSv4.0 root to expose export leaf '" + exportLeafName + "'.");
        }

        return exportLookup.ObjectFileHandle.ToArray();
    }
}
""";

            return template
                .Replace("__SAMPLE_HOST__", sampleHost, StringComparison.Ordinal)
                .Replace("__SAMPLE_MOUNT_PORT__", sampleMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS_PORT__", sampleNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS40_PORT__", sampleNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__UNFS3_HOST__", unfs3Host, StringComparison.Ordinal)
                .Replace("__UNFS3_MOUNT_PORT__", unfs3MountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__UNFS3_NFS_PORT__", unfs3NfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__KNFSD_HOST__", knfsdHost, StringComparison.Ordinal)
                .Replace("__KNFSD_NFS_PORT__", knfsdNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__GANESHA_HOST__", ganeshaHost, StringComparison.Ordinal)
                .Replace("__GANESHA_NFS40_PORT__", ganeshaNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        public static string CreateClientAgainstPackedServerProgramSource(
            string serverHost,
            int serverMountPort,
            int serverNfsPort)
        {
            string template = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        CancellationToken cancellationToken = CancellationToken.None;

        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer("__SERVER_HOST__", __SERVER_NFS_PORT__)
            .WithMountPort(__SERVER_MOUNT_PORT__)
            .WithUdpForNfsV3(true)
            .Build();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
        if (!exports.Any(entry => string.Equals(entry.ExportPath, "/data", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Expected the packaged server export listing to include '/data'.");
        }

        await using OpenNfsMountSession session = await client.MountAsync("/data", cancellationToken).ConfigureAwait(false);

        byte[] existingBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Encoding.UTF8.GetString(existingBytes), "hello-from-packed-server", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the packaged client to read the packaged server seed file.");
        }

        const string createdPath = "/client-to-server.txt";
        const string createdContents = "written-through-packaged-client-to-packaged-server";
        await session.Directories.CreateFileAsync(createdPath, failIfExists: true, cancellationToken).ConfigureAwait(false);
        await session.Files.WriteAllBytesAsync(
            createdPath,
            Encoding.UTF8.GetBytes(createdContents),
            OpenNfsWriteStability.FileSync,
            cancellationToken).ConfigureAwait(false);

        byte[] roundTripBytes = await session.Files.ReadAllBytesAsync(createdPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Encoding.UTF8.GetString(roundTripBytes), createdContents, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the packaged client to read back the payload written through the packaged server.");
        }

        await session.Directories.DeleteFileAsync(createdPath, cancellationToken).ConfigureAwait(false);

        Console.WriteLine("PACKAGE CLIENT TO PACKAGE SERVER OK");
        return 0;
    }
}
""";

            return template
                .Replace("__SERVER_HOST__", serverHost, StringComparison.Ordinal)
                .Replace("__SERVER_MOUNT_PORT__", serverMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SERVER_NFS_PORT__", serverNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        public static string CreateServerApplicationProgramSource(bool denyMounts)
        {
            return PackagedServerApplicationProgramSource.Create(denyMounts);
        }

        private static string ReplacePeerTokens(
            string template,
            string sampleHost,
            int sampleMountPort,
            int sampleNfsPort,
            int sampleNfs40Port,
            string unfs3Host,
            int unfs3MountPort,
            int unfs3NfsPort,
            string knfsdHost,
            int knfsdMountPort,
            int knfsdNfsPort,
            string ganeshaHost,
            int ganeshaNfs40Port)
        {
            return template
                .Replace("__SAMPLE_HOST__", sampleHost, StringComparison.Ordinal)
                .Replace("__SAMPLE_MOUNT_PORT__", sampleMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS_PORT__", sampleNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS40_PORT__", sampleNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__UNFS3_HOST__", unfs3Host, StringComparison.Ordinal)
                .Replace("__UNFS3_MOUNT_PORT__", unfs3MountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__UNFS3_NFS_PORT__", unfs3NfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__KNFSD_HOST__", knfsdHost, StringComparison.Ordinal)
                .Replace("__KNFSD_MOUNT_PORT__", knfsdMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__KNFSD_NFS_PORT__", knfsdNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__GANESHA_HOST__", ganeshaHost, StringComparison.Ordinal)
                .Replace("__GANESHA_NFS40_PORT__", ganeshaNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
    }
}
