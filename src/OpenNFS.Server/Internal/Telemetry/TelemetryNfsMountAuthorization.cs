namespace OpenNFS.Server.Internal.Telemetry
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Decorates a host-supplied <see cref="INfsMountAuthorization"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsMountAuthorization : INfsMountAuthorization
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityMountAuthorization;

        private readonly INfsMountAuthorization _Inner;

        internal TelemetryNfsMountAuthorization(INfsMountAuthorization inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsMountAuthorization Inner => _Inner;

        public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "authorize", _Inner, request, static (host, value) => host.AuthorizeAsync(value));
        }
    }
}
