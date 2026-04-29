namespace OpenNFS.Client
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides path-first metadata helpers over an <see cref="OpenNfsMountSession"/>.
    /// </summary>
    public sealed class OpenNfsMountSessionMetadata
    {
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
            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, "Mounted-session attribute read", cancellationToken).ConfigureAwait(false);
            OpenNfsV3GetAttributesResult result =
                await _session.Client.Files.GetAttributesV3Async(fileHandle, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess || result.Attributes is null)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session attribute read", path, result.Status);
            }

            return result.Attributes;
        }
    }
}
