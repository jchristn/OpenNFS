namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides grouped owner and owner-group identity helpers over the current public client surface.
    /// </summary>
    public sealed class OpenNfsIdentityService
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsIdentityService(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Maps server-returned owner and owner-group strings through the configured client identity policy.
        /// </summary>
        /// <param name="serverOwner">Server-returned owner string.</param>
        /// <param name="serverOwnerGroup">Server-returned owner-group string.</param>
        /// <returns>The mapped identity values.</returns>
        public OpenNfsMappedIdentity Map(string serverOwner, string serverOwnerGroup)
        {
            return _client.Settings.IdentityPolicy.Map(serverOwner, serverOwnerGroup);
        }

        /// <summary>
        /// Executes an NFSv4.0 owner and owner-group <c>GETATTR</c> flow and maps the decoded identity strings through the configured client identity policy.
        /// </summary>
        /// <param name="fileHandle">Target filehandle.</param>
        /// <param name="cancellationToken">Cancellation token for the read.</param>
        /// <returns>The typed owner and owner-group result.</returns>
        public async Task<OpenNfsV40GetIdentityResult> GetOwnerAndGroupV40Async(
            byte[] fileHandle,
            CancellationToken cancellationToken)
        {
            OpenNfsV40GetAttributesResult attributesResult = await _client.Files.GetAttributesV40Async(
                fileHandle,
                new[]
                {
                    OpenNfsV40AttributeKind.Owner,
                    OpenNfsV40AttributeKind.OwnerGroup,
                },
                cancellationToken).ConfigureAwait(false);

            if (!attributesResult.IsSuccess)
            {
                return new OpenNfsV40GetIdentityResult(attributesResult.Status);
            }

            OpenNfsV40Attributes attributes = attributesResult.Attributes
                ?? throw new OpenNfsClientProtocolException(
                    "NFSv4.0 owner and owner-group GETATTR succeeded without returning the attribute envelope.",
                    "NFSv4.0 GETATTR owner/owner_group",
                    OpenNfsErrorCategory.ProtocolError,
                    isRetryable: false,
                    innerException: null);

            if (string.IsNullOrWhiteSpace(attributes.Owner) || string.IsNullOrWhiteSpace(attributes.OwnerGroup))
            {
                throw new OpenNfsClientProtocolException(
                    "NFSv4.0 owner and owner-group GETATTR succeeded without returning both identity strings.",
                    "NFSv4.0 GETATTR owner/owner_group",
                    OpenNfsErrorCategory.ProtocolError,
                    isRetryable: false,
                    innerException: null);
            }

            return new OpenNfsV40GetIdentityResult(
                attributesResult.Status,
                Map(attributes.Owner, attributes.OwnerGroup));
        }

        /// <summary>
        /// Executes an NFSv4.0 owner and owner-group <c>SETATTR</c> flow and rereads the effective identity mapping on success.
        /// </summary>
        /// <param name="fileHandle">Target filehandle.</param>
        /// <param name="owner">Replacement owner string, or <c>null</c> to preserve the current owner.</param>
        /// <param name="ownerGroup">Replacement owner-group string, or <c>null</c> to preserve the current owner-group.</param>
        /// <param name="cancellationToken">Cancellation token for the update.</param>
        /// <returns>The typed owner and owner-group update result.</returns>
        public async Task<OpenNfsV40SetIdentityResult> SetOwnerAndGroupV40Async(
            byte[] fileHandle,
            string? owner,
            string? ownerGroup,
            CancellationToken cancellationToken)
        {
            OpenNfsV40SetIdentityResult setResult = await _client.Files.SetOwnerAndGroupV40Async(
                fileHandle,
                owner,
                ownerGroup,
                cancellationToken).ConfigureAwait(false);

            if (!setResult.IsSuccess)
            {
                return setResult;
            }

            OpenNfsV40GetIdentityResult rereadResult = await GetOwnerAndGroupV40Async(fileHandle, cancellationToken).ConfigureAwait(false);
            return new OpenNfsV40SetIdentityResult(
                setResult.Status,
                setResult.SetAttributeMaskWords,
                rereadResult.Identity);
        }
    }
}
