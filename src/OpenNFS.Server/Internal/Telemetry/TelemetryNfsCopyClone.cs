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
    /// Decorates a host-supplied <see cref="INfsCopyClone"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsCopyClone : INfsCopyClone
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityCopyClone;

        private readonly INfsCopyClone _Inner;

        internal TelemetryNfsCopyClone(INfsCopyClone inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsCopyClone Inner => _Inner;

        public ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "copy", _Inner, request, static (host, value) => host.CopyAsync(value));
        }

        public ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "clone", _Inner, request, static (host, value) => host.CloneAsync(value));
        }
    }
}
