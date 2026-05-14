namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using static Nfs40CompoundMetadataResults;

    internal sealed class Nfs40CompoundVerifyOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundVerifyOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleVerifyAsync(
            fattr4? requestedAttributes,
            Nfs40CompoundState state,
            bool expectMatch,
            CancellationToken cancellationToken)
        {
            Func<nfsstat4, Nfs40CompoundOperationResult> createResult = expectMatch
                ? CreateVerifyResult
                : CreateNotVerifyResult;

            if (!Nfs40AttributeEncoder.TryValidateAttributePayload(
                requestedAttributes,
                includeIdentityAttributes: _server.Capabilities.IdMapper is not null,
                includeAclAttributes: _server.Capabilities.Acls is not null,
                out nfsstat4 validationStatus))
            {
                return createResult(validationStatus);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return createResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return createResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            TryCreateAttributesResult attributeResult =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    refreshedHandle,
                    requestedAttributes!.attrmask,
                    cancellationToken).ConfigureAwait(false);
            if (attributeResult.Attributes is null)
            {
                return createResult(attributeResult.ErrorStatus);
            }

            byte[] expectedBytes = requestedAttributes.attr_vals?.Value ?? Array.Empty<byte>();
            byte[] actualBytes = attributeResult.Attributes.attr_vals?.Value ?? Array.Empty<byte>();
            return createResult(CompareVerifiedAttributes(expectedBytes, actualBytes, expectMatch));
        }

        private static nfsstat4 CompareVerifiedAttributes(byte[] expectedBytes, byte[] actualBytes, bool expectMatch)
        {
            bool isSame = expectedBytes.AsSpan().SequenceEqual(actualBytes);
            if (expectMatch)
            {
                return isSame ? nfsstat4.NFS4_OK : nfsstat4.NFS4ERR_NOT_SAME;
            }

            return isSame ? nfsstat4.NFS4ERR_SAME : nfsstat4.NFS4_OK;
        }
    }
}
