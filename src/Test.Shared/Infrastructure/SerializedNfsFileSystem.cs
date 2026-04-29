namespace Test.Shared.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class SerializedNfsFileSystem : INfsFileSystem
    {
        private readonly INfsFileSystem _inner;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        internal SerializedNfsFileSystem(INfsFileSystem inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
        }

        public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            return ExecuteAsync(() => _inner.CommitFileAsync(request), request.CancellationToken);
        }

        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            return ExecuteAsync(() => _inner.CreateHardLinkAsync(request), request.CancellationToken);
        }

        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            return ExecuteAsync(() => _inner.CreatePathAsync(request), request.CancellationToken);
        }

        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            return ExecuteAsync(() => _inner.CreateSymbolicLinkAsync(request), request.CancellationToken);
        }

        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            return ExecuteAsync(() => _inner.DeletePathAsync(request), request.CancellationToken);
        }

        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            return ExecuteAsync(() => _inner.GetPathInfoAsync(request), request.CancellationToken);
        }

        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            return ExecuteAsync(() => _inner.LookupPathAsync(request), request.CancellationToken);
        }

        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            return ExecuteAsync(() => _inner.ReadDirectoryAsync(request), request.CancellationToken);
        }

        public Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            return ExecuteAsync(() => _inner.ReadFileAsync(request), request.CancellationToken);
        }

        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            return ExecuteAsync(() => _inner.ReadSymbolicLinkAsync(request), request.CancellationToken);
        }

        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            return ExecuteAsync(() => _inner.RenamePathAsync(request), request.CancellationToken);
        }

        public Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            return ExecuteAsync(() => _inner.WriteFileAsync(request), request.CancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(Func<Task<T>> executeAsync, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executeAsync);

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await executeAsync().ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
