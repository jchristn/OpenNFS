namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    /// <summary>
    /// Shared helpers for the public OpenNFS server surface suite catalog.
    /// </summary>
    internal static class ServerSurfaceSuiteSupport
    {
        internal static Task ExecuteApplicationSurfaceTryLifecycleCompanionsAndTypedFailuresAsync(CancellationToken cancellationToken)
        {
            return ServerSurfaceApplicationSupport.ExecuteApplicationSurfaceTryLifecycleCompanionsAndTypedFailuresAsync(cancellationToken);
        }

        internal static Task ExecuteBuiltApplicationServesV3AndV40FlowsAsync(CancellationToken cancellationToken)
        {
            return ServerSurfaceApplicationSupport.ExecuteBuiltApplicationServesV3AndV40FlowsAsync(cancellationToken);
        }

        internal static Task ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync(CancellationToken cancellationToken)
        {
            return ServerSurfaceApplicationSupport.ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync(cancellationToken);
        }

        internal static Task ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ServerSurfacePackagingSupport.ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ServerSurfacePackagingSupport.ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedServerPackagePreservesNegativeExportValidationAsync(CancellationToken cancellationToken)
        {
            return ServerSurfacePackagingSupport.ExecutePackedServerPackagePreservesNegativeExportValidationAsync(cancellationToken);
        }

        internal static Task AssertServerApplicationServesV3AndV40FlowsAsync(
            string host,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            string exportPath,
            string v40ExportLeafName,
            string expectedHelloContents,
            CancellationToken cancellationToken)
        {
            return ServerSurfaceRuntimeSupport.AssertServerApplicationServesV3AndV40FlowsAsync(
                host,
                mountPort,
                nfsPort,
                nfs40Port,
                exportPath,
                v40ExportLeafName,
                expectedHelloContents,
                cancellationToken);
        }

        internal static Task<byte[]> ResolveExportRootV40Async(
            OpenNfsClient client,
            string exportLeafName,
            CancellationToken cancellationToken)
        {
            return ServerSurfaceRuntimeSupport.ResolveExportRootV40Async(client, exportLeafName, cancellationToken);
        }
    }
}
