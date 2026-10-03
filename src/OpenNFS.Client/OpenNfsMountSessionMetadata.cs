namespace OpenNFS.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Provides path-first metadata helpers over an <see cref="OpenNfsMountSession"/>.
    /// </summary>
    public sealed class OpenNfsMountSessionMetadata
    {
        private const string SetAttributesOperationName = "Mounted-session attribute update";

        private readonly OpenNfsMountSession _session;

        internal OpenNfsMountSessionMetadata(OpenNfsMountSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Gets the NFSv3 attributes for the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative file or directory path.</param>
        /// <param name="cancellationToken">Cancellation token for the attribute read.</param>
        /// <returns>The decoded NFSv3 attributes.</returns>
        public async Task<OpenNfsV3Attributes> GetAttributesAsync(string path, CancellationToken cancellationToken)
        {
            OpenNfsClientOperation? telemetry = OpenNfsClientInstrumentation.StartSessionOperation("GetAttributesAsync");
            try
            {
                OpenNfsV3Attributes result = await GetAttributesCoreAsync(path, cancellationToken).ConfigureAwait(false);
                telemetry?.Succeed();
                return result;
            }
            catch (Exception exception)
            {
                telemetry?.Fail(exception);
                throw;
            }
        }

        private async Task<OpenNfsV3Attributes> GetAttributesCoreAsync(string path, CancellationToken cancellationToken)
        {
            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, "Mounted-session attribute read", cancellationToken).ConfigureAwait(false);
            OpenNfsV3GetAttributesResult result =
                await _session.Client.Files.GetAttributesV3Async(fileHandle, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess || result.Attributes is null)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session attribute read", path, result.Status);
            }

            return result.Attributes;
        }

        /// <summary>
        /// Determines whether any file system entry (file, directory, symbolic link, or other object) exists at the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative path. The export root (<c>/</c>) always exists.</param>
        /// <param name="cancellationToken">Cancellation token for the lookup.</param>
        /// <returns>
        /// <c>true</c> when the entry exists; <c>false</c> when the server reports <see cref="OpenNfsV3Status.NoEntry"/> for the entry or any
        /// intermediate directory, or <see cref="OpenNfsV3Status.NotDirectory"/> because an intermediate component is not a directory.
        /// </returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown for any other failure status (for example access denied or a stale handle).</exception>
        public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            OpenNfsClientOperation? telemetry = OpenNfsClientInstrumentation.StartSessionOperation("ExistsAsync");
            try
            {
                bool result = await ExistsCoreAsync(path, cancellationToken).ConfigureAwait(false);
                telemetry?.Succeed();
                return result;
            }
            catch (Exception exception)
            {
                telemetry?.Fail(exception);
                throw;
            }
        }

        private async Task<bool> ExistsCoreAsync(string path, CancellationToken cancellationToken)
        {
            OpenNfsMountSessionResolution resolution = await _session.ResolvePathAsync(path, cancellationToken).ConfigureAwait(false);
            switch (resolution.Status)
            {
                case OpenNfsV3Status.Ok:
                    return true;

                case OpenNfsV3Status.NoEntry:
                case OpenNfsV3Status.NotDirectory:
                    return false;

                default:
                    throw OpenNfsMountSession.CreateStatusException("Mounted-session existence check", path, resolution.Status);
            }
        }

        /// <summary>
        /// Sets the size of the file at the specified export-relative path through NFSv3 <c>SETATTR</c>.
        /// A smaller size truncates the file; a larger size extends it with zero bytes.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="length">New file size in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the update.</param>
        /// <returns>A task that completes when the size has been updated.</returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved or the server rejects the size change.</exception>
        public async Task SetLengthAsync(string path, ulong length, CancellationToken cancellationToken)
        {
            OpenNfsClientOperation? telemetry = OpenNfsClientInstrumentation.StartSessionOperation("SetLengthAsync");
            try
            {
                await SetLengthCoreAsync(path, length, cancellationToken).ConfigureAwait(false);
                telemetry?.Succeed();
            }
            catch (Exception exception)
            {
                telemetry?.Fail(exception);
                throw;
            }
        }

        private async Task SetLengthCoreAsync(string path, ulong length, CancellationToken cancellationToken)
        {
            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, SetAttributesOperationName, cancellationToken).ConfigureAwait(false);
            await SetAttributesAsync(fileHandle, path, new OpenNfsV3SetAttributes(sizeBytes: length), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the access and/or modification timestamps of the entry at the specified export-relative path through NFSv3 <c>SETATTR</c>.
        /// </summary>
        /// <param name="path">Export-relative file or directory path.</param>
        /// <param name="accessTimeUtc">
        /// New access time, or <c>null</c> to leave it unchanged. <see cref="DateTimeKind.Local"/> values are converted to UTC;
        /// <see cref="DateTimeKind.Unspecified"/> values are treated as UTC.
        /// </param>
        /// <param name="modifyTimeUtc">
        /// New modification time, or <c>null</c> to leave it unchanged. Conversion rules match <paramref name="accessTimeUtc"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for the update.</param>
        /// <returns>A task that completes when the timestamps have been updated. No SETATTR is sent when both values are <c>null</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a timestamp is outside the NFSv3 representable range.</exception>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved or the server rejects the change.</exception>
        public async Task SetTimesAsync(string path, DateTime? accessTimeUtc, DateTime? modifyTimeUtc, CancellationToken cancellationToken)
        {
            OpenNfsClientOperation? telemetry = OpenNfsClientInstrumentation.StartSessionOperation("SetTimesAsync");
            try
            {
                await SetTimesCoreAsync(path, accessTimeUtc, modifyTimeUtc, cancellationToken).ConfigureAwait(false);
                telemetry?.Succeed();
            }
            catch (Exception exception)
            {
                telemetry?.Fail(exception);
                throw;
            }
        }

        private async Task SetTimesCoreAsync(string path, DateTime? accessTimeUtc, DateTime? modifyTimeUtc, CancellationToken cancellationToken)
        {
            OpenNfsV3Time? accessTime = accessTimeUtc.HasValue ? OpenNfsV3Time.FromDateTimeUtc(accessTimeUtc.Value) : null;
            OpenNfsV3Time? modifyTime = modifyTimeUtc.HasValue ? OpenNfsV3Time.FromDateTimeUtc(modifyTimeUtc.Value) : null;

            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, SetAttributesOperationName, cancellationToken).ConfigureAwait(false);
            if (accessTime is null && modifyTime is null)
            {
                return;
            }

            OpenNfsV3SetAttributes attributes = new OpenNfsV3SetAttributes(
                accessTimeMode: accessTime is null ? OpenNfsV3TimeSetMode.DoNotChange : OpenNfsV3TimeSetMode.SetToClientTime,
                accessTime: accessTime,
                modifyTimeMode: modifyTime is null ? OpenNfsV3TimeSetMode.DoNotChange : OpenNfsV3TimeSetMode.SetToClientTime,
                modifyTime: modifyTime);
            await SetAttributesAsync(fileHandle, path, attributes, cancellationToken).ConfigureAwait(false);
        }

        private async Task SetAttributesAsync(
            byte[] fileHandle,
            string path,
            OpenNfsV3SetAttributes attributes,
            CancellationToken cancellationToken)
        {
            OpenNfsV3SetAttributesResult result =
                await _session.Client.Files.SetAttributesV3Async(fileHandle, attributes, guardChangeTime: null, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException(SetAttributesOperationName, path, result.Status);
            }
        }
    }
}
