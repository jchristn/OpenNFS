namespace OpenNFS.Server.Internal.Telemetry
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Decorates a host-supplied <see cref="INfsDelegations"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsDelegations : INfsDelegations
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityDelegations;

        private readonly INfsDelegations _Inner;

        internal TelemetryNfsDelegations(INfsDelegations inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsDelegations Inner => _Inner;

        public Task<NfsAcquireDelegationResponse> AcquireDelegationAsync(NfsAcquireDelegationRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "acquire_delegation", _Inner, request, static (host, value) => host.AcquireDelegationAsync(value));
        }

        public Task RecallDelegationAsync(NfsRecallDelegationRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "recall_delegation", _Inner, request, static (host, value) => host.RecallDelegationAsync(value));
        }

        public Task ReturnDelegationAsync(NfsReturnDelegationRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "return_delegation", _Inner, request, static (host, value) => host.ReturnDelegationAsync(value));
        }
    }
}
