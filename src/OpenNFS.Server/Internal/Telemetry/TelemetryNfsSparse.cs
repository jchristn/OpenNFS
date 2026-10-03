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
    /// Decorates a host-supplied <see cref="INfsSparse"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsSparse : INfsSparse
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilitySparse;

        private readonly INfsSparse _Inner;

        internal TelemetryNfsSparse(INfsSparse inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsSparse Inner => _Inner;

        public ValueTask<NfsSeekResponse> SeekAsync(NfsSeekRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "seek", _Inner, request, static (host, value) => host.SeekAsync(value));
        }

        public ValueTask<NfsAllocateResponse> AllocateAsync(NfsAllocateRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "allocate", _Inner, request, static (host, value) => host.AllocateAsync(value));
        }

        public ValueTask<NfsDeallocateResponse> DeallocateAsync(NfsDeallocateRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "deallocate", _Inner, request, static (host, value) => host.DeallocateAsync(value));
        }

        public ValueTask<NfsReadSparseResponse> ReadSparseAsync(NfsReadSparseRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendValueAsync(Capability, "read_sparse", _Inner, request, static (host, value) => host.ReadSparseAsync(value));
        }
    }
}
