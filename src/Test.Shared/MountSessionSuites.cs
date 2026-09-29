namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the v0.1.1 mounted-session client surface, NFSv3/NFSv4.0 SETATTR, the attribute-mutation
    /// server capability, and portmapper discovery against in-process OpenNFS servers.
    /// </summary>
    public static class MountSessionSuites
    {
        private const string SuiteId = "MountSessionSuites";

        /// <summary>
        /// Creates the mounted-session suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Mounted Session, SETATTR, and Portmapper",
                cases: new List<TestCaseDescriptor>
                {
                    Unit("OpenNfsV3TimeConvertsToAndFromDateTime", "OpenNfsV3Time converts to and from UTC DateTime values with tick precision and range checks", MountSessionUnitSupport.ExecuteTimeConversionsRoundTripAsync),
                    Unit("SetAttributesModelValidatesTimeModes", "OpenNfsV3SetAttributes validates client-time and server-time combinations", MountSessionUnitSupport.ExecuteSetAttributesModelValidatesAsync),
                    Unit("SetAttributesPlanEncodesSattr3AndDecodesReplies", "SETATTR plans encode sattr3 plus the ctime guard and replies decode status and wcc data", MountSessionUnitSupport.ExecuteSetAttributesPlanAndDecodeAsync),
                    Unit("ClientXidSeedsAreRandomizedPerInstance", "Clients created back-to-back start from independent random xids so server duplicate-request caches cannot replay another client's reply", MountSessionUnitSupport.ExecuteClientXidSeedsAreRandomizedAsync),
                    Unit("TransferSizesAreSelectedFromFsInfo", "Mounted-session transfer sizes use FSINFO preferred sizes bounded by the maximums with a 64 KiB fallback", MountSessionUnitSupport.ExecuteTransferSizesAreSelectedFromFsInfoAsync),
                    Unit("ReadCountsAreAlignedToPages", "Chunked READ counts are shortened so unaligned reads end on a 4 KiB page boundary (avoids the knfsd oversized-reply socket shutdown)", MountSessionUnitSupport.ExecuteAlignedReadCountAsync),
                    Unit("LocalFileSystemAdvertisesAttributeMutation", "UseLocalFileSystem registers the INfsAttributeMutation capability and hosts without it keep it absent", MountSessionUnitSupport.ExecuteLocalFileSystemAdvertisesAttributeMutationAsync),
                    Unit("PortmapperGetPortCodecRoundTrips", "PMAPPROC_GETPORT calls encode the XDR mapping and replies decode the port", MountSessionUnitSupport.ExecuteGetPortCodecRoundTripsAsync),
                    Unit("PortmapperSettingsDefaultToDisabled", "Portmapper discovery is disabled by default, captures its port, and validates input", MountSessionUnitSupport.ExecutePortmapperSettingsDefaultAndValidateAsync),

                    Integration("MountSessionCoreScenariosAgainstOpenNfsServer", "Mounted-session create, truncate, ranged read, stream read/write, rename, exists, SETATTR, and cancellation scenarios pass against an in-process OpenNFS server", MountSessionIntegrationSupport.ExecuteCoreScenariosAgainstOpenNfsServerAsync),
                    Integration("MountSessionListsLargeDirectoryWithAttributes", "ListWithAttributesAsync pages 1,500 entries through READDIRPLUS with attributes for every entry", MountSessionIntegrationSupport.ExecuteLargeDirectoryAgainstOpenNfsServerAsync),
                    Integration("MountSessionConcurrentOperationsAreSafe", "32 parallel writes, reads, lookups, and listings on one mounted session return correct results", MountSessionIntegrationSupport.ExecuteConcurrencyAgainstOpenNfsServerAsync),
                    Integration("MountAsyncSessionDisposeUnmounts", "Disposing a MountAsync session sends UMNT, is idempotent, and never throws", MountSessionIntegrationSupport.ExecuteMountAsyncSessionDisposeUnmountsAsync),
                    Integration("EphemeralServerSnippetBindsLoopbackPorts", "The README ephemeral-server snippet binds loopback MOUNT and NFS ports, serves a directory, and stops cleanly", MountSessionIntegrationSupport.ExecuteEphemeralServerSnippetAsync),
                    Integration("FsInfoAdvertisesCanSetTime", "FSINFO advertises FSF3_CANSETTIME when the host supports attribute mutation", MountSessionIntegrationSupport.ExecuteFsInfoAdvertisesCanSetTimeAsync),
                    Integration("ShortWritesAreContinued", "WriteAllBytesAsync and WriteAsync continue NFSv3 short writes from offset plus count", MountSessionFaultInjectionSupport.ExecuteShortWritesAreContinuedAsync),
                    Integration("ZeroProgressWriteIsRejected", "A WRITE acknowledging zero bytes is reported as a protocol error instead of looping", MountSessionFaultInjectionSupport.ExecuteZeroProgressWriteIsRejectedAsync),
                    Integration("UnstableWritesAreCommitted", "UNSTABLE and DATA_SYNC writes are followed by one COMMIT while FILE_SYNC writes are not", MountSessionFaultInjectionSupport.ExecuteUnstableWritesAreCommittedAsync),
                    Integration("CommitVerifierChangeTriggersRewrite", "A COMMIT verifier that differs from the WRITE verifier triggers a FILE_SYNC rewrite, or a clear failure for non-seekable streams", MountSessionFaultInjectionSupport.ExecuteVerifierChangeTriggersRewriteAsync),
                    Integration("WriteVerifierChangeBetweenWritesTriggersRewrite", "A write verifier that changes between UNSTABLE writes triggers a FILE_SYNC rewrite", MountSessionFaultInjectionSupport.ExecuteWriteVerifierChangeBetweenWritesTriggersRewriteAsync),
                    Integration("PersistentVerifierChangeFailsClearly", "A verifier that keeps changing after the FILE_SYNC rewrite fails with a clear OpenNfsClientIoException", MountSessionFaultInjectionSupport.ExecutePersistentVerifierChangeFailsClearlyAsync),
                    Integration("TransferSizesFollowSmallFsInfoLimits", "A server advertising wtmax 4096 and rtmax 1024 bounds every WRITE and READ", MountSessionFaultInjectionSupport.ExecuteTransferSizesFollowFsInfoAsync),
                    Integration("UnalignedReadsStayWithinTransferPages", "The seek-heavy stream read sequence and unaligned ranged reads never issue a READ spanning more pages than an aligned transfer-size READ", MountSessionFaultInjectionSupport.ExecuteUnalignedReadsStayWithinTransferPagesAsync),
                    Integration("TransferSizesFallBackWhenFsInfoFails", "A failed FSINFO falls back to 64 KiB transfers", MountSessionFaultInjectionSupport.ExecuteTransferSizesFallBackWhenFsInfoFailsAsync),
                    Integration("ListWithAttributesFallsBackToReaddir", "ListWithAttributesAsync falls back to READDIR plus LOOKUP when READDIRPLUS returns NOTSUPP", MountSessionFaultInjectionSupport.ExecuteListWithAttributesFallsBackToReaddirAsync),
                    Integration("ListWithAttributesCompletesMissingAttributes", "ListWithAttributesAsync completes entries whose READDIRPLUS attributes or handles were omitted", MountSessionFaultInjectionSupport.ExecuteListWithAttributesCompletesMissingAttributesAsync),
                    Integration("MidFlightCancellationSurfacesOperationCanceled", "Cancelling an in-flight RPC surfaces OperationCanceledException rather than a protocol exception", MountSessionFaultInjectionSupport.ExecuteMidFlightCancellationSurfacesOperationCanceledAsync),
                    Integration("SetAttributesRejectedWithoutHostCapability", "Hosts without INfsAttributeMutation keep rejecting size and mode SETATTR with NFS3ERR_NOTSUPP", MountSessionFaultInjectionSupport.ExecuteSetAttributesWithoutHostCapabilityAsync),
                    Integration("V40SetAttributesTruncatesThroughCapability", "NFSv4.0 SETATTR size and time_modify_set are applied through INfsAttributeMutation", MountSessionV40SetAttributesSupport.ExecuteV40SetAttributesTruncatesThroughCapabilityAsync),
                    Integration("PooledConnectionsReuseForSequentialRpcs", "2,000+ sequential RPCs reuse a single pooled TCP connection instead of one connection per RPC", MountSessionTransportSupport.ExecuteSequentialRpcsReuseConnectionsAsync),
                    Integration("PooledConnectionsBoundConcurrentRpcs", "32-way concurrent mounted-session I/O multiplexes over at most the per-endpoint connection limit", MountSessionTransportSupport.ExecuteConcurrentRpcsShareBoundedConnectionsAsync),
                    Integration("PooledConnectionsReconnectAfterServerRestart", "After a server restart the next RPC reconnects transparently, including non-idempotent writes", MountSessionTransportSupport.ExecuteServerRestartReconnectsAsync),
                    Integration("PooledConnectionKillFailsInFlightRpcsFast", "Killing a pooled connection fails its in-flight RPCs promptly with OpenNfsClientIoException and the next RPC reconnects", MountSessionTransportSupport.ExecuteKilledConnectionFailsInFlightFastAsync),
                    Integration("PooledConnectionCancellationIsPerRpc", "Cancelling one RPC does not disturb other RPCs sharing its connection, and its late reply is discarded", MountSessionTransportSupport.ExecuteCancellingOneRpcKeepsOthersAsync),
                    Integration("PooledConnectionResponseTimeoutOnBlackHole", "The response timeout bounds RPCs on a black-holed connection and the connection is replaced", MountSessionTransportSupport.ExecuteResponseTimeoutOnBlackHoledConnectionAsync),
                    Integration("PooledConnectionsCloseWhenIdle", "Idle pooled connections close after the idle timeout and on DisconnectAsync", MountSessionTransportSupport.ExecuteIdleConnectionsCloseAsync),
                    Integration("CreateAndMkdirSendDefaultModes", "CREATE and MKDIR carry the default 0644/0755 modes and the in-process server honors a configured create mode", MountSessionPermissionSupport.ExecuteInProcessCreateModesAsync),
                    Integration("PortmapperDiscoveryFindsMountAndNfsPorts", "Portmapper discovery finds the MOUNT v3 and NFSv3 ports when they are not configured", MountSessionIntegrationSupport.ExecutePortmapperDiscoveryFindsPortsAsync),
                    Integration("PortmapperDiscoveryExplicitEndpointsWin", "Explicit mount and NFS endpoints take precedence over portmapper results", MountSessionIntegrationSupport.ExecutePortmapperExplicitEndpointsWinAsync),
                    Integration("PortmapperDiscoveryUnreachableFallsBack", "An unreachable portmapper falls back to configured endpoints and a failing mount explains why", MountSessionIntegrationSupport.ExecutePortmapperUnreachableFallsBackAsync),
                    Integration("PortmapperDiscoveryZeroPortFallsBack", "A portmapper reporting port 0 falls back and the mount failure explains that MOUNT is not registered", MountSessionIntegrationSupport.ExecutePortmapperZeroPortFallsBackAsync),
                });
        }

        private static TestCaseDescriptor Unit(string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId: SuiteId,
                caseId: caseId,
                displayName: displayName,
                tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                executeAsync: executeAsync);
        }

        private static TestCaseDescriptor Integration(string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId: SuiteId,
                caseId: caseId,
                displayName: displayName,
                tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                executeAsync: executeAsync);
        }
    }
}
