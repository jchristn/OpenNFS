namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    internal static class OpenNfsRpcCredentialFactory
    {
        internal static opaque_auth CreateRpcCredential(
            OpenNfsClientSettings settings,
            OpenNfsAuthenticationFlavor authenticationFlavor,
            uint xid)
        {
            ArgumentNullException.ThrowIfNull(settings);

            switch (authenticationFlavor)
            {
                case OpenNfsAuthenticationFlavor.AuthNone:
                    return RpcAuthenticationCodec.CreateNone();
                case OpenNfsAuthenticationFlavor.AuthSys:
                    return CreateAuthSysCredential(settings, xid);
                case OpenNfsAuthenticationFlavor.RpcSecGss:
                    throw new OpenNfsClientProtocolException(
                        "RPCSEC_GSS is not available on the current public OpenNFS client surface. "
                        + "See README.md for the remaining security work.",
                        contextName: nameof(OpenNfsAuthenticationFlavor.RpcSecGss),
                        category: OpenNfsErrorCategory.Unsupported,
                        isRetryable: false,
                        innerException: null);
                default:
                    throw new OpenNfsClientProtocolException(
                        "The client execution path does not support authentication flavor '"
                        + authenticationFlavor.ToString() + "'.",
                        contextName: nameof(authenticationFlavor),
                        category: OpenNfsErrorCategory.Unsupported,
                        isRetryable: false,
                        innerException: null);
            }
        }

        private static opaque_auth CreateAuthSysCredential(OpenNfsClientSettings settings, uint xid)
        {
            OpenNfsAuthSysCredentials credentials = settings.AuthSysCredentials;
            return RpcAuthenticationCodec.CreateSystem(
                new authsys_parms
                {
                    stamp = xid,
                    machinename = credentials.MachineName,
                    uid = credentials.UserId,
                    gid = credentials.GroupId,
                    gids = CopySupplementaryGroupIds(credentials.SupplementaryGroupIds),
                });
        }

        private static uint[] CopySupplementaryGroupIds(IReadOnlyList<uint> supplementaryGroupIds)
        {
            if (supplementaryGroupIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] copy = new uint[supplementaryGroupIds.Count];
            for (int index = 0; index < supplementaryGroupIds.Count; index++)
            {
                copy[index] = supplementaryGroupIds[index];
            }

            return copy;
        }
    }
}
