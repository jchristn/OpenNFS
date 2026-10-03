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
    /// Decorates a host-supplied <see cref="INfsAcls"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsAcls : INfsAcls
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityAcls;

        private readonly INfsAcls _Inner;

        internal TelemetryNfsAcls(INfsAcls inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsAcls Inner => _Inner;

        public Task<NfsGetAclResponse> GetAclAsync(NfsGetAclRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "get_acl", _Inner, request, static (host, value) => host.GetAclAsync(value));
        }

        public Task<NfsSetAclResponse> SetAclAsync(NfsSetAclRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "set_acl", _Inner, request, static (host, value) => host.SetAclAsync(value));
        }
    }
}
