namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V40.Hosting;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropSuiteSupport;
    /// <summary>
    /// Shared execution helpers for Linux userspace-server NFSv4.0 interop flows.
    /// </summary>
    internal static class InteropLinuxUserspaceV40Support
    {
        internal static async Task ExecuteClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxNfsV40ServerContainer server =
                await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);

                OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                    exportRootHandle,
                    0UL,
                    new byte[8],
                    4096U,
                    cancellationToken).ConfigureAwait(false);
                if (!rootListing.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 READDIR against the Linux server export root to succeed, but received status '"
                        + rootListing.Status.ToString()
                        + "'.");
                }

                    string[] rootNames = rootListing.Entries
                        .Select(static entry => entry.Name)
                        .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                        .OrderBy(static name => name, StringComparer.Ordinal)
                        .ToArray();
                    if (!rootNames.SequenceEqual(new[] { "d", "h.txt" }))
                    {
                        throw new InvalidOperationException(
                            "Expected the Linux NFSv4.0 server root to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", rootNames) + ".");
                    }

                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);
                if (!fileLookup.IsSuccess || fileLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 LOOKUP for 'h.txt' against the Linux server export root to succeed, but received status '"
                        + fileLookup.Status.ToString()
                        + "'.");
                }

                byte[] fileHandle = fileLookup.ObjectFileHandle.ToArray();
                OpenNfsV40ReadResult initialRead = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                if (!initialRead.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(initialRead.Data.Span), "0123456789ABCDEF", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server file to contain the seeded interop payload.");
                }

                byte[] clientVerifier = new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-v40-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-v40-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenConfirmationResult confirmedOpen =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateId openStateId = confirmedOpen.StateId;
                uint closeSequenceId = confirmedOpen.NextSequenceId;

                byte[] updatedBytes = Encoding.UTF8.GetBytes("FEDCBA9876543210");
                OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                    fileHandle,
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    updatedBytes,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                    fileHandle,
                    0UL,
                    (uint)updatedBytes.Length,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileHandle,
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);

                if (!setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !writeResult.IsSuccess
                    || writeResult.Count != (uint)updatedBytes.Length
                    || !commitResult.IsSuccess
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "FEDCBA9876543210", StringComparison.Ordinal)
                    || !closeResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server flow to support grouped OPEN, WRITE, COMMIT, READ, and CLOSE successfully.");
                }

                DockerCommandResult exportedFile = await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "exec",
                        server.ContainerName,
                        "cat",
                        "/export-real/h.txt",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                if (!string.Equals(exportedFile.StandardOutput.Trim(), "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server export file to reflect the bytes written through the grouped client flow.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        internal static async Task ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxNfsV40ServerContainer server =
                await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);

                OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "missing.txt",
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                    exportRootHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);

                byte[] clientVerifier = new byte[] { 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-v40-negative-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-v40-negative-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenConfirmationResult confirmedOpen =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateId openStateId = confirmedOpen.StateId;
                uint closeSequenceId = confirmedOpen.NextSequenceId;
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40WriteResult staleWriteResult = await client.Files.WriteV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    Encoding.UTF8.GetBytes("stale-stateid"),
                    cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                    || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                    || !fileLookup.IsSuccess
                    || !setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !closeResult.IsSuccess
                    || staleWriteResult.Status != OpenNfsV40Status.BadStateId)
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 negative path to preserve NOENT, ISDIR, and BAD_STATEID behavior.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }
    }
}

