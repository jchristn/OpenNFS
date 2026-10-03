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
    /// Decorates a host-supplied <see cref="INfsAttributeMutation"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsAttributeMutation : INfsAttributeMutation
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityAttributeMutation;

        private readonly INfsAttributeMutation _Inner;

        internal TelemetryNfsAttributeMutation(INfsAttributeMutation inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsAttributeMutation Inner => _Inner;

        public Task<NfsSetAttributesResponse> SetAttributesAsync(NfsSetAttributesRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "set_attributes", _Inner, request, static (host, value) => host.SetAttributesAsync(value));
        }
    }
}
