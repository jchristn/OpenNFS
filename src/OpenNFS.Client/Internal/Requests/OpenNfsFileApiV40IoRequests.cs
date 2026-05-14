namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40RequestSupport;

    internal static class OpenNfsFileApiV40IoRequests
    {
        internal static OpenNfsCompoundRequest CreateReadLinkV40Request(byte[] symbolicLinkHandle)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "readlink",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(symbolicLinkHandle, nameof(symbolicLinkHandle)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_READLINK, Array.Empty<byte>()),
                });
        }

        internal static OpenNfsCompoundRequest CreateReadV40Request(byte[] fileHandle, ulong offset, uint count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested NFSv4 READ byte count must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "read",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_READ,
                        EncodeV40Payload(
                            new READ4args
                            {
                                stateid = CreateAnonymousStateId(),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                count = new count4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateWriteV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId stateId,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data)
        {
            byte[] safeData = OpenNfsClientArgument.RequireBytes(data, nameof(data), allowEmpty: true);
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "write",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_WRITE,
                        EncodeV40Payload(
                            new WRITE4args
                            {
                                stateid = CreateStateId(stateId, nameof(stateId)),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                stable = CreateWriteStability(stability),
                                data = safeData,
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCommitV40Request(byte[] fileHandle, ulong offset, uint count)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "commit",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_COMMIT,
                        EncodeV40Payload(
                            new COMMIT4args
                            {
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                count = new count4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }
    }
}
