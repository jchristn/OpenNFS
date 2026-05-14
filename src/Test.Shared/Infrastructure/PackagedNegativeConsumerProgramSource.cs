namespace Test.Shared.Infrastructure
{
    using System;
    using System.Globalization;

    internal static class PackagedNegativeConsumerProgramSource
    {
        internal static string Create(
            string sampleHost,
            int sampleMountPort,
            int sampleNfsPort,
            int sampleNfs40Port,
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
                .Replace("__KNFSD_HOST__", knfsdHost, StringComparison.Ordinal)
                .Replace("__KNFSD_NFS_PORT__", knfsdNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__GANESHA_HOST__", ganeshaHost, StringComparison.Ordinal)
                .Replace("__GANESHA_NFS40_PORT__", ganeshaNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
    }
}
