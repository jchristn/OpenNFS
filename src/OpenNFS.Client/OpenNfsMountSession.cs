namespace OpenNFS.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;

    /// <summary>
    /// Represents an export-scoped client session for path-first NFSv3 file and directory work.
    /// Every member is safe to call concurrently from multiple threads.
    /// </summary>
    public sealed class OpenNfsMountSession : IDisposable, IAsyncDisposable
    {
        private static readonly TimeSpan UnmountOnDisposeTimeout = TimeSpan.FromSeconds(5);

        private readonly OpenNfsClient _client;
        private readonly bool _ownsMount;
        private readonly byte[] _rootFileHandle;
        private readonly object _transferSizesSyncRoot = new object();
        private int _disposed;
        private Task<OpenNfsMountSessionTransferSizes>? _transferSizesTask;

        internal OpenNfsMountSession(OpenNfsClient client, string exportPath, byte[] rootFileHandle)
            : this(client, exportPath, rootFileHandle, ownsMount: false)
        {
        }

        internal OpenNfsMountSession(OpenNfsClient client, string exportPath, byte[] rootFileHandle, bool ownsMount)
        {
            ArgumentNullException.ThrowIfNull(client);
            _client = client;
            _ownsMount = ownsMount;
            ExportPath = OpenNfsClientArgument.RequireText(exportPath, nameof(exportPath));
            _rootFileHandle = OpenNfsClientArgument.RequireBytes(rootFileHandle, nameof(rootFileHandle), allowEmpty: false);
            Files = new OpenNfsMountSessionFiles(this);
            Directories = new OpenNfsMountSessionDirectories(this);
            Metadata = new OpenNfsMountSessionMetadata(this);
        }

        /// <summary>
        /// Gets the mounted export path represented by this session.
        /// </summary>
        public string ExportPath { get; }

        /// <summary>
        /// Gets the export root filehandle bytes associated with this session.
        /// </summary>
        public ReadOnlyMemory<byte> RootFileHandle
        {
            get
            {
                return new ReadOnlyMemory<byte>(_rootFileHandle.AsSpan().ToArray());
            }
        }

        /// <summary>
        /// Gets the grouped path-first file APIs for this mounted export.
        /// </summary>
        public OpenNfsMountSessionFiles Files { get; }

        /// <summary>
        /// Gets the grouped path-first directory APIs for this mounted export.
        /// </summary>
        public OpenNfsMountSessionDirectories Directories { get; }

        /// <summary>
        /// Gets the grouped path-first metadata APIs for this mounted export.
        /// </summary>
        public OpenNfsMountSessionMetadata Metadata { get; }

        /// <summary>
        /// Gets the current advanced locking surface.
        /// Path-first lock helpers remain deferred while the client still requires explicit lock-program endpoint selection.
        /// </summary>
        public Apis.LockApis Locks
        {
            get
            {
                return _client.Locks;
            }
        }

        /// <summary>
        /// Releases mounted-session resources synchronously.
        /// Sessions created by <see cref="OpenNfsClient.MountAsync(string, CancellationToken)"/> send a best-effort MOUNT v3
        /// <c>UMNT</c> for the export; failures are ignored and this method never throws.
        /// The mounted-session abstraction does not own the underlying client lifetime.
        /// </summary>
        public void Dispose()
        {
            try
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Releases mounted-session resources asynchronously.
        /// Sessions created by <see cref="OpenNfsClient.MountAsync(string, CancellationToken)"/> send a best-effort MOUNT v3
        /// <c>UMNT</c> for the export; failures are ignored and this method never throws.
        /// The mounted-session abstraction does not own the underlying client lifetime.
        /// </summary>
        /// <returns>A task that completes when disposal has finished.</returns>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0 || !_ownsMount)
            {
                return;
            }

            if (_client.State != OpenNfsClientState.Open)
            {
                return;
            }

            try
            {
                using CancellationTokenSource timeoutTokenSource = new CancellationTokenSource(UnmountOnDisposeTimeout);
                await _client.Exports.UnmountV3Async(ExportPath, timeoutTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        internal OpenNfsClient Client
        {
            get
            {
                return _client;
            }
        }

        internal Task<OpenNfsMountSessionTransferSizes> GetTransferSizesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_transferSizesSyncRoot)
            {
                if (_transferSizesTask is null
                    || _transferSizesTask.IsFaulted
                    || _transferSizesTask.IsCanceled)
                {
                    _transferSizesTask = LoadTransferSizesAsync();
                }

                return _transferSizesTask.WaitAsync(cancellationToken);
            }
        }

        internal async Task<byte[]> ResolvePathHandleOrThrowAsync(string path, string operationName, CancellationToken cancellationToken)
        {
            string[] pathComponents = NormalizePathComponents(path, allowRootPath: true);
            OpenNfsMountSessionResolution resolution = await ResolvePathAsync(pathComponents, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != OpenNfsV3Status.Ok)
            {
                throw CreateStatusException(operationName, path, resolution.Status);
            }

            return resolution.FileHandle;
        }

        internal async Task<OpenNfsMountSessionResolution> ResolvePathAsync(string path, CancellationToken cancellationToken)
        {
            string[] pathComponents = NormalizePathComponents(path, allowRootPath: true);
            return await ResolvePathAsync(pathComponents, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<OpenNfsMountSessionResolution> ResolvePathWithAttributesOrThrowAsync(
            string path,
            string operationName,
            CancellationToken cancellationToken)
        {
            string[] pathComponents = NormalizePathComponents(path, allowRootPath: true);
            OpenNfsMountSessionResolution resolution = await ResolvePathAsync(pathComponents, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != OpenNfsV3Status.Ok)
            {
                throw CreateStatusException(operationName, path, resolution.Status);
            }

            if (resolution.Attributes is not null)
            {
                return resolution;
            }

            OpenNfsV3GetAttributesResult attributesResult =
                await _client.Files.GetAttributesV3Async(resolution.FileHandle, cancellationToken).ConfigureAwait(false);
            if (!attributesResult.IsSuccess || attributesResult.Attributes is null)
            {
                throw CreateStatusException(operationName, path, attributesResult.Status);
            }

            return new OpenNfsMountSessionResolution(OpenNfsV3Status.Ok, resolution.FileHandle, attributesResult.Attributes);
        }

        internal async Task<(byte[] ParentHandle, string EntryName)> ResolveParentOrThrowAsync(
            string path,
            string operationName,
            CancellationToken cancellationToken)
        {
            string[] pathComponents = NormalizePathComponents(path, allowRootPath: false);
            if (pathComponents.Length == 0)
            {
                throw new ArgumentException("The path must identify an entry beneath the mounted export root.", nameof(path));
            }

            string entryName = pathComponents[pathComponents.Length - 1];
            string[] parentComponents = new string[pathComponents.Length - 1];
            if (parentComponents.Length > 0)
            {
                Array.Copy(pathComponents, parentComponents, parentComponents.Length);
            }

            OpenNfsMountSessionResolution resolution = await ResolvePathAsync(parentComponents, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != OpenNfsV3Status.Ok)
            {
                throw CreateStatusException(operationName, path, resolution.Status);
            }

            return (resolution.FileHandle, entryName);
        }

        internal static string[] SplitPath(string path, bool allowRootPath)
        {
            return NormalizePathComponents(path, allowRootPath);
        }

        internal static OpenNfsV3StatusException CreateStatusException(string operationName, string path, OpenNfsV3Status status)
        {
            return new OpenNfsV3StatusException(operationName, path, status);
        }

        private static string[] NormalizePathComponents(string path, bool allowRootPath)
        {
            string normalizedPath = OpenNfsClientArgument.RequireText(path, nameof(path)).Replace('\\', '/');
            // Components are used verbatim: leading, trailing, and repeated spaces are legal in NFS names, so only the empty
            // components produced by leading, trailing, or repeated separators are dropped.
            string[] components = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (components.Length == 0)
            {
                if (allowRootPath)
                {
                    return Array.Empty<string>();
                }

                throw new ArgumentException("The path must identify an entry beneath the mounted export root.", nameof(path));
            }

            for (int index = 0; index < components.Length; index++)
            {
                string component = components[index];
                if (string.Equals(component, ".", StringComparison.Ordinal)
                    || string.Equals(component, "..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Relative path navigation segments '.' and '..' are not supported in mounted-session paths.", nameof(path));
                }
            }

            return components;
        }

        private async Task<OpenNfsMountSessionResolution> ResolvePathAsync(
            string[] pathComponents,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] currentHandle = _rootFileHandle.AsSpan().ToArray();
            OpenNfsV3Attributes? currentAttributes = null;
            if (pathComponents.Length == 0)
            {
                return new OpenNfsMountSessionResolution(OpenNfsV3Status.Ok, currentHandle, currentAttributes);
            }

            for (int index = 0; index < pathComponents.Length; index++)
            {
                OpenNfsV3LookupResult lookupResult =
                    await _client.Directories.LookupV3Async(currentHandle, pathComponents[index], cancellationToken).ConfigureAwait(false);

                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    OpenNfsV3Status failureStatus = lookupResult.IsSuccess ? OpenNfsV3Status.ServerFault : lookupResult.Status;
                    return new OpenNfsMountSessionResolution(failureStatus, Array.Empty<byte>(), null);
                }

                currentHandle = lookupResult.ObjectFileHandle.ToArray();
                currentAttributes = lookupResult.ObjectAttributes;
            }

            return new OpenNfsMountSessionResolution(OpenNfsV3Status.Ok, currentHandle, currentAttributes);
        }

        private async Task<OpenNfsMountSessionTransferSizes> LoadTransferSizesAsync()
        {
            bool datagramCapable = _client.Settings.TransportPolicy == OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3;

            try
            {
                OpenNfsV3FileSystemInfoResult fileSystemInfo =
                    await _client.Files.GetFileSystemInfoV3Async(_rootFileHandle.AsSpan().ToArray(), _client.LifetimeCancellationToken).ConfigureAwait(false);
                if (!fileSystemInfo.IsSuccess)
                {
                    return OpenNfsMountSessionTransferSizes.CreateDefault(datagramCapable);
                }

                return OpenNfsMountSessionTransferSizes.FromFileSystemInfo(fileSystemInfo, datagramCapable);
            }
            catch (OpenNfsClientException)
            {
                return OpenNfsMountSessionTransferSizes.CreateDefault(datagramCapable);
            }
        }
    }
}
