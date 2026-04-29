namespace OpenNFS.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Immutable server wrapper that currently exposes validated configuration and export resolution state.
    /// </summary>
    public sealed class OpenNfsServer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServer"/> class.
        /// </summary>
        /// <param name="settings">Validated server settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public OpenNfsServer(OpenNfsServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Settings = settings;
        }

        /// <summary>
        /// Gets the immutable settings associated with this server wrapper.
        /// </summary>
        public OpenNfsServerSettings Settings { get; }

        /// <summary>
        /// Gets the optional capability catalog associated with this server wrapper.
        /// </summary>
        public NfsServerCapabilities Capabilities
        {
            get
            {
                return Settings.Capabilities;
            }
        }

        /// <summary>
        /// Creates or reuses a stable filehandle for a host-visible target.
        /// </summary>
        /// <param name="target">Filehandle target to encode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created or reused stable filehandle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public Task<NfsFileHandle> CreateFileHandleAsync(NfsFileHandleTarget target, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(target);
            return CreateFileHandleCoreAsync(new NfsCreateFileHandleRequest(target, cancellationToken));
        }

        /// <summary>
        /// Creates or reuses a stable filehandle using an explicit request context.
        /// </summary>
        /// <param name="request">Request context for the filehandle-creation operation.</param>
        /// <returns>The response context containing the created or reused stable filehandle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configured filehandle provider returns null.</exception>
        public async Task<NfsCreateFileHandleResponse> CreateFileHandleAsync(NfsCreateFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            NfsCreateFileHandleResponse? response =
                await Settings.FileHandleProvider.CreateAsync(request).ConfigureAwait(false);

            if (response is null)
            {
                throw new InvalidOperationException("The configured filehandle provider returned null instead of a filehandle response.");
            }

            return response;
        }

        /// <summary>
        /// Resolves a previously created server-side filehandle.
        /// </summary>
        /// <param name="fileHandle">Filehandle to resolve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The filehandle resolution result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileHandle"/> is null.</exception>
        public Task<NfsFileHandleResolution> ResolveFileHandleAsync(NfsFileHandle fileHandle, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            return ResolveFileHandleCoreAsync(new NfsResolveFileHandleRequest(fileHandle, cancellationToken));
        }

        /// <summary>
        /// Resolves a previously created server-side filehandle using an explicit request context.
        /// </summary>
        /// <param name="request">Request context for the filehandle-resolution operation.</param>
        /// <returns>The response context containing the filehandle resolution result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configured filehandle provider returns null.</exception>
        public async Task<NfsResolveFileHandleResponse> ResolveFileHandleAsync(NfsResolveFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            NfsResolveFileHandleResponse? response =
                await Settings.FileHandleProvider.ResolveAsync(request).ConfigureAwait(false);

            if (response is null)
            {
                throw new InvalidOperationException("The configured filehandle provider returned null instead of a filehandle-resolution response.");
            }

            return response;
        }

        /// <summary>
        /// Resolves and validates the exports exposed by the configured host surface.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validated export definitions.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configured export provider returns invalid entries, duplicate export paths, missing source paths, or non-directory source paths.
        /// </exception>
        public async Task<IReadOnlyList<OpenNfsExportDefinition>> GetExportsAsync(CancellationToken cancellationToken)
        {
            NfsGetExportsResponse response =
                await GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);

            return response.Exports;
        }

        /// <summary>
        /// Resolves and validates the exports exposed by the configured host surface using an explicit request context.
        /// </summary>
        /// <param name="request">Request context for the export-resolution operation.</param>
        /// <returns>The response context containing the validated export definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configured export provider returns invalid entries, duplicate export paths, missing source paths, or non-directory source paths.
        /// </exception>
        public async Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            NfsGetExportsResponse? providerResponse =
                await Settings.ExportProvider.GetExportsAsync(request).ConfigureAwait(false);

            if (providerResponse is null)
            {
                throw new InvalidOperationException("The configured export provider returned null instead of an export response.");
            }

            HashSet<string> exportPaths = new HashSet<string>(StringComparer.Ordinal);
            List<OpenNfsExportDefinition> validatedExports = new List<OpenNfsExportDefinition>(providerResponse.Exports.Count);

            foreach (OpenNfsExportDefinition? exportDefinition in providerResponse.Exports)
            {
                request.CancellationToken.ThrowIfCancellationRequested();

                if (exportDefinition is null)
                {
                    throw new InvalidOperationException("The configured export provider returned a null export definition.");
                }

                if (!exportPaths.Add(exportDefinition.ExportPath))
                {
                    throw new InvalidOperationException("The configured export path '" + exportDefinition.ExportPath + "' was returned more than once.");
                }

                NfsGetPathInfoResponse? pathInfoResponse =
                    await Settings.FileSystem.GetPathInfoAsync(
                        new NfsGetPathInfoRequest(exportDefinition.SourcePath, request.CancellationToken)).ConfigureAwait(false);

                if (pathInfoResponse is null)
                {
                    throw new InvalidOperationException("The configured file system returned null instead of a path-info response for source path '" + exportDefinition.SourcePath + "'.");
                }

                NfsPathInfo pathInfo = pathInfoResponse.PathInfo;

                if (!pathInfo.Exists)
                {
                    throw new InvalidOperationException("The configured export '" + exportDefinition.ExportPath + "' maps to missing source path '" + exportDefinition.SourcePath + "'.");
                }

                if (pathInfo.Kind != NfsPathKind.Directory)
                {
                    throw new InvalidOperationException(
                        "The configured export '" + exportDefinition.ExportPath + "' must map to a directory source path, but '" + exportDefinition.SourcePath + "' resolved as " + GetPathKindDescription(pathInfo.Kind) + ".");
                }

                validatedExports.Add(exportDefinition);
            }

            return new NfsGetExportsResponse(validatedExports);
        }

        private async Task<NfsFileHandle> CreateFileHandleCoreAsync(NfsCreateFileHandleRequest request)
        {
            NfsCreateFileHandleResponse response = await CreateFileHandleAsync(request).ConfigureAwait(false);
            return response.FileHandle;
        }

        private async Task<NfsFileHandleResolution> ResolveFileHandleCoreAsync(NfsResolveFileHandleRequest request)
        {
            NfsResolveFileHandleResponse response = await ResolveFileHandleAsync(request).ConfigureAwait(false);
            return response.Resolution;
        }

        private static string GetPathKindDescription(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => "a directory",
                NfsPathKind.File => "a file",
                NfsPathKind.SymbolicLink => "a symbolic link",
                NfsPathKind.Other => "an unsupported path kind",
                _ => "a missing path",
            };
        }
    }
}
