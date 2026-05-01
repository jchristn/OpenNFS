namespace OpenNFS.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;

    /// <summary>
    /// Represents an export-scoped client session for path-first NFSv3 file and directory work.
    /// </summary>
    public sealed class OpenNfsMountSession : IDisposable, IAsyncDisposable
    {
        private readonly OpenNfsClient _client;
        private readonly byte[] _rootFileHandle;

        internal OpenNfsMountSession(OpenNfsClient client, string exportPath, byte[] rootFileHandle)
        {
            ArgumentNullException.ThrowIfNull(client);
            _client = client;
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
        /// The current mounted-session abstraction does not own the underlying client lifetime.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Releases mounted-session resources asynchronously.
        /// The current mounted-session abstraction does not own the underlying client lifetime.
        /// </summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        internal OpenNfsClient Client
        {
            get
            {
                return _client;
            }
        }

        internal async Task<byte[]> ResolvePathHandleOrThrowAsync(string path, string operationName, CancellationToken cancellationToken)
        {
            string[] pathComponents = NormalizePathComponents(path, allowRootPath: true);
            (OpenNfsV3Status Status, byte[] FileHandle) resolution = await ResolvePathHandleAsync(pathComponents, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != OpenNfsV3Status.Ok)
            {
                throw CreateStatusException(operationName, path, resolution.Status);
            }

            return resolution.FileHandle;
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

            (OpenNfsV3Status Status, byte[] FileHandle) resolution = await ResolvePathHandleAsync(parentComponents, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != OpenNfsV3Status.Ok)
            {
                throw CreateStatusException(operationName, path, resolution.Status);
            }

            return (resolution.FileHandle, entryName);
        }

        internal static OpenNfsV3StatusException CreateStatusException(string operationName, string path, OpenNfsV3Status status)
        {
            return new OpenNfsV3StatusException(operationName, path, status);
        }

        private static string[] NormalizePathComponents(string path, bool allowRootPath)
        {
            string normalizedPath = OpenNfsClientArgument.RequireText(path, nameof(path)).Replace('\\', '/');
            string[] components = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

        private async Task<(OpenNfsV3Status Status, byte[] FileHandle)> ResolvePathHandleAsync(
            string[] pathComponents,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] currentHandle = _rootFileHandle.AsSpan().ToArray();
            if (pathComponents.Length == 0)
            {
                return (OpenNfsV3Status.Ok, currentHandle);
            }

            for (int index = 0; index < pathComponents.Length; index++)
            {
                OpenNfsV3LookupResult lookupResult =
                    await _client.Directories.LookupV3Async(currentHandle, pathComponents[index], cancellationToken).ConfigureAwait(false);

                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    return (lookupResult.Status, Array.Empty<byte>());
                }

                currentHandle = lookupResult.ObjectFileHandle.ToArray();
            }

            return (OpenNfsV3Status.Ok, currentHandle);
        }
    }
}
