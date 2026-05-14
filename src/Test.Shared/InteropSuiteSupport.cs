namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the Linux and peer interop suite catalog.
    /// </summary>
    internal static class InteropSuiteSupport
    {
        internal static OpenNfsServer CreateOpenNfsInteropServer(
            string mappingPath,
            string sourceRoot,
            StaticMountAuthorization? authorization = null)
        {
            return InteropServerSupport.CreateOpenNfsInteropServer(mappingPath, sourceRoot, authorization);
        }

        internal static Task<OpenNfsServer> CreateOpenNfsV40InteropServerAsync(
            CancellationToken cancellationToken,
            bool includeAcls,
            bool includeDelegations)
        {
            return InteropServerSupport.CreateOpenNfsV40InteropServerAsync(cancellationToken, includeAcls, includeDelegations);
        }

        internal static Task<byte[]> ResolveSampleExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            return InteropSampleV40Support.ResolveSampleExportRootV40Async(client, cancellationToken);
        }

        internal static string CreateLinuxMountCommand(int mountPort, int nfsPort)
        {
            return InteropShellCommandSupport.CreateLinuxMountCommand(mountPort, nfsPort);
        }

        internal static string CreateLinuxReadOnlyMountCommand(
            int mountPort,
            int nfsPort,
            string exportPath,
            string primaryFilePath,
            string nestedFilePath)
        {
            return InteropShellCommandSupport.CreateLinuxReadOnlyMountCommand(
                mountPort,
                nfsPort,
                exportPath,
                primaryFilePath,
                nestedFilePath);
        }

        internal static string CreateLinuxSampleMountCommand(int mountPort, int nfsPort)
        {
            return InteropShellCommandSupport.CreateLinuxSampleMountCommand(mountPort, nfsPort);
        }

        internal static void CreateLinuxServerExportLayout(string exportDirectory)
        {
            InteropServerSupport.CreateLinuxServerExportLayout(exportDirectory);
        }

        internal static Task<OpenNfsV40LookupResult> WaitForLinuxV40RootAsync(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            return InteropLinuxWaitSupport.WaitForLinuxV40RootAsync(client, cancellationToken);
        }

        internal static Task<OpenNfsV3LookupResult> WaitForV3LookupAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return InteropLinuxWaitSupport.WaitForV3LookupAsync(client, directoryHandle, entryName, cancellationToken);
        }

        internal static Task<byte[]> ResolveLinuxExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            return InteropLinuxWaitSupport.ResolveLinuxExportRootV40Async(client, cancellationToken);
        }

        internal static Task<OpenNfsMountV3Result> WaitForLinuxMountV3Async(
            string host,
            int mountPort,
            string exportPath,
            bool enableUdpForNfsV3,
            CancellationToken cancellationToken)
        {
            return InteropLinuxWaitSupport.WaitForLinuxMountV3Async(
                host,
                mountPort,
                exportPath,
                enableUdpForNfsV3,
                cancellationToken);
        }

        internal static Task<byte[]> ResolveLinuxExportRootV40Async(
            string host,
            int nfsPort,
            CancellationToken cancellationToken)
        {
            return InteropLinuxWaitSupport.ResolveLinuxExportRootV40Async(host, nfsPort, cancellationToken);
        }

        internal static Task<OpenNfsV40OpenResult> WaitForV40OpenReadyAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            OpenNfsV40OpenResult initialResult,
            CancellationToken cancellationToken)
        {
            return InteropOpenStateSupport.WaitForV40OpenReadyAsync(
                client,
                directoryHandle,
                clientId,
                openOwner,
                entryName,
                shareAccess,
                shareDeny,
                sequenceId,
                initialResult,
                cancellationToken);
        }

        internal static Task<OpenConfirmationResult> ConfirmOpenIfRequiredAsync(
            OpenNfsClient client,
            OpenNfsV40OpenResult openResult,
            uint confirmSequenceId,
            CancellationToken cancellationToken)
        {
            return InteropOpenStateSupport.ConfirmOpenIfRequiredAsync(client, openResult, confirmSequenceId, cancellationToken);
        }

        internal readonly struct OpenConfirmationResult
        {
            internal OpenConfirmationResult(OpenNfsV40StateId stateId, uint nextSequenceId)
            {
                StateId = stateId;
                NextSequenceId = nextSequenceId;
            }

            internal OpenNfsV40StateId StateId { get; }

            internal uint NextSequenceId { get; }
        }

        internal static string CreateTempDirectory()
        {
            return InteropServerSupport.CreateTempDirectory();
        }

        internal static void DeleteDirectoryIfPresent(string directoryPath)
        {
            InteropServerSupport.DeleteDirectoryIfPresent(directoryPath);
        }
    }
}
