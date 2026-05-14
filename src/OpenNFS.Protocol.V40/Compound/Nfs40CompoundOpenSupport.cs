namespace OpenNFS.Protocol.V40.Compound
{
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server.Delegations;

    internal static class Nfs40CompoundOpenSupport
    {
        internal static open_delegation4 CreateDelegation(Nfs40DelegationState delegationState)
        {
            return delegationState.DelegationKind switch
            {
                NfsDelegationKind.Read => new open_delegation4
                {
                    delegation_type = open_delegation_type4.OPEN_DELEGATE_READ,
                    read = new open_read_delegation4
                    {
                        stateid = delegationState.StateId,
                        recall = delegationState.RecallRequested,
                        permissions = CreateDelegationPermissionsAce(),
                    },
                },
                NfsDelegationKind.Write => new open_delegation4
                {
                    delegation_type = open_delegation_type4.OPEN_DELEGATE_WRITE,
                    write = new open_write_delegation4
                    {
                        stateid = delegationState.StateId,
                        recall = delegationState.RecallRequested,
                        space_limit = new nfs_space_limit4
                        {
                            limitby = limit_by4.NFS_LIMIT_SIZE,
                            filesize = ulong.MaxValue,
                        },
                        permissions = CreateDelegationPermissionsAce(),
                    },
                },
                _ => CreateNoDelegation(),
            };
        }

        internal static open_delegation4 CreateNoDelegation()
        {
            return new open_delegation4
            {
                delegation_type = open_delegation_type4.OPEN_DELEGATE_NONE,
            };
        }

        internal static ulong GetClientId(OPEN4args arguments)
        {
            return arguments.owner?.clientid?.Value ?? 0UL;
        }

        internal static bool HasRequestedAttributes(fattr4? attributes)
        {
            if (attributes?.attrmask?.Value is uint[] attributeMaskWords)
            {
                for (int index = 0; index < attributeMaskWords.Length; index++)
                {
                    if (attributeMaskWords[index] != 0U)
                    {
                        return true;
                    }
                }
            }

            return attributes?.attr_vals?.Value is { Length: > 0 };
        }

        private static nfsace4 CreateDelegationPermissionsAce()
        {
            return new nfsace4
            {
                type = new acetype4
                {
                    Value = (uint)Nfs40Constants.ACE4_ACCESS_ALLOWED_ACE_TYPE,
                },
                flag = new aceflag4
                {
                    Value = 0U,
                },
                access_mask = new acemask4
                {
                    Value = 0xFFFFFFFFU,
                },
                who = new utf8str_mixed
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes("EVERYONE@"),
                    },
                },
            };
        }
    }
}
