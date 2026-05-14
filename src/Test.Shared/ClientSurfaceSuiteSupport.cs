namespace Test.Shared
{
    using System.ComponentModel;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared helpers for the public OpenNFS client builder and lifetime suite catalog.
    /// </summary>
    internal static class ClientSurfaceSuiteSupport
    {
        internal static Task ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceMountedSessionSupport.ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync(cancellationToken);
        }

        internal static Task ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceMountedSessionSupport.ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync(cancellationToken);
        }

        internal static Task ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceMountedSessionSupport.ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync(cancellationToken);
        }

        internal static Task ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceMountedSessionSupport.ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync(cancellationToken);
        }

        internal static Task ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static void AssertEditorBrowsableState(MethodInfo? methodInfo, EditorBrowsableState expectedState, string displayName)
        {
            ClientSurfacePackagingSupport.AssertEditorBrowsableState(methodInfo, expectedState, displayName);
        }

        internal static Task ExecuteMountAsyncUsesDedicatedMountEndpointAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteMountAsyncUsesDedicatedMountEndpointAsync(cancellationToken);
        }

        internal static Task ExecuteRpcSecGssAuthOnlyMountAgainstKerberosProbeAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteRpcSecGssAuthOnlyMountAgainstKerberosProbeAsync(cancellationToken);
        }

        internal static Task ExecuteRawV42CompoundAgainstPublicServerApplicationAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteRawV42CompoundAgainstPublicServerApplicationAsync(cancellationToken);
        }

        internal static Task ExecuteGroupedV42FileApisAgainstPublicServerApplicationAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteGroupedV42FileApisAgainstPublicServerApplicationAsync(cancellationToken);
        }

        internal static Task ExecuteGroupedV42FileApisReuseSessionAcrossCallsAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteGroupedV42FileApisReuseSessionAcrossCallsAsync(cancellationToken);
        }

        internal static Task ExecuteGroupedV42FileApisReconnectAfterTransportBreakAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteGroupedV42FileApisReconnectAfterTransportBreakAsync(cancellationToken);
        }

        internal static Task ExecuteMountAsyncThrowsForDeniedMountAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteMountAsyncThrowsForDeniedMountAsync(cancellationToken);
        }

        internal static Task ExecuteTypedCredentialMountAsyncRejectsFlavorMismatchAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteTypedCredentialMountAsyncRejectsFlavorMismatchAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageSupportsRpcSecGssConfigurationFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageSupportsRpcSecGssConfigurationFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageSupportsRawNfs42CompoundFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageSupportsRawNfs42CompoundFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageSupportsGroupedNfs42PlanningFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageSupportsGroupedNfs42PlanningFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageReusesGroupedNfs42SessionFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageReusesGroupedNfs42SessionFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageReconnectsGroupedNfs42SessionAfterTransportBreakFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackageReconnectsGroupedNfs42SessionAfterTransportBreakFromCleanConsumerAppAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync(CancellationToken cancellationToken)
        {
            return ClientSurfacePackagingSupport.ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync(cancellationToken);
        }

        internal static Task ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync(CancellationToken cancellationToken)
        {
            return ClientSurfaceRuntimeSupport.ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync(cancellationToken);
        }
    }
}
