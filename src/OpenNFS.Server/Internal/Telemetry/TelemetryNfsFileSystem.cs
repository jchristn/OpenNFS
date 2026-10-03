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
    /// Decorates a host-supplied <see cref="INfsFileSystem"/> so every call records an <c>opennfs.server.backend.duration</c>
    /// measurement and a <c>&lt;capability&gt; &lt;operation&gt;</c> span. Protocol handlers call through this decorator; the public
    /// settings and capability properties keep returning the host's original instance.
    /// </summary>
    /// <remarks>Thread safe when the decorated instance is.</remarks>
    internal sealed class TelemetryNfsFileSystem : INfsFileSystem
    {
        private const string Capability = OpenNfsTelemetryNames.CapabilityFileSystem;

        private readonly INfsFileSystem _Inner;

        internal TelemetryNfsFileSystem(INfsFileSystem inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        internal INfsFileSystem Inner => _Inner;

        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "get_path_info", _Inner, request, static (host, value) => host.GetPathInfoAsync(value));
        }

        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "lookup_path", _Inner, request, static (host, value) => host.LookupPathAsync(value));
        }

        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "read_directory", _Inner, request, static (host, value) => host.ReadDirectoryAsync(value));
        }

        public async Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            NfsReadFileResponse response = await OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "read_file", _Inner, request, static (host, value) => host.ReadFileAsync(value)).ConfigureAwait(false);
            if (response is not null)
            {
                OpenNfsServerInstrumentation.RecordIoBytes(OpenNfsTelemetryNames.DirectionRead, response.Data.Length);
            }

            return response!;
        }

        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "read_symbolic_link", _Inner, request, static (host, value) => host.ReadSymbolicLinkAsync(value));
        }

        public async Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            NfsWriteFileResponse response = await OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "write_file", _Inner, request, static (host, value) => host.WriteFileAsync(value)).ConfigureAwait(false);
            if (response is not null)
            {
                OpenNfsServerInstrumentation.RecordIoBytes(OpenNfsTelemetryNames.DirectionWrite, response.BytesWritten);
            }

            return response!;
        }

        public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "commit_file", _Inner, request, static (host, value) => host.CommitFileAsync(value));
        }

        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "create_path", _Inner, request, static (host, value) => host.CreatePathAsync(value));
        }

        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "create_symbolic_link", _Inner, request, static (host, value) => host.CreateSymbolicLinkAsync(value));
        }

        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "create_hard_link", _Inner, request, static (host, value) => host.CreateHardLinkAsync(value));
        }

        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "delete_path", _Inner, request, static (host, value) => host.DeletePathAsync(value));
        }

        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            return OpenNfsServerInstrumentation.TrackBackendAsync(Capability, "rename_path", _Inner, request, static (host, value) => host.RenamePathAsync(value));
        }
    }
}
