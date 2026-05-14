namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40RequestSupport;

    internal static class OpenNfsFileApiV40StateRequests
    {
        internal static OpenNfsCompoundRequest CreateOpenExistingV40Request(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateOpenArguments(
                                clientId,
                                openOwner,
                                entryName,
                                shareAccess,
                                shareDeny,
                                sequenceId,
                                opentype4.OPEN4_NOCREATE,
                                failIfExists: false).WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCreateAndOpenV40Request(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-create",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateOpenArguments(
                                clientId,
                                openOwner,
                                entryName,
                                shareAccess,
                                shareDeny,
                                sequenceId,
                                opentype4.OPEN4_CREATE,
                                failIfExists).WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateReclaimOpenV40Request(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-reclaim",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateReclaimOpenArguments(
                                clientId,
                                openOwner,
                                shareAccess,
                                shareDeny,
                                sequenceId).WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateOpenConfirmV40Request(OpenNfsV40StateId openStateId, uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-confirm",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_CONFIRM,
                        EncodeV40Payload(
                            new OPEN_CONFIRM4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateOpenConfirmV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-confirm",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_CONFIRM,
                        EncodeV40Payload(
                            new OPEN_CONFIRM4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateOpenDowngradeV40Request(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-downgrade",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_DOWNGRADE,
                        EncodeV40Payload(
                            new OPEN_DOWNGRADE4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                share_access = (uint)shareAccess,
                                share_deny = (uint)shareDeny,
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCloseV40Request(OpenNfsV40StateId openStateId, uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "close",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CLOSE,
                        EncodeV40Payload(
                            new CLOSE4args
                            {
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCloseV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "close",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CLOSE,
                        EncodeV40Payload(
                            new CLOSE4args
                            {
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateDelegationReturnV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "delegreturn",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_DELEGRETURN,
                        EncodeV40Payload(
                            new DELEGRETURN4args
                            {
                                deleg_stateid = CreateStateId(delegationStateId, nameof(delegationStateId)),
                            }.WriteTo)),
                });
        }
    }
}
