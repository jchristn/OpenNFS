namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V42.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV42RequestSupport;

    internal static class OpenNfsFileApiV42Requests
    {
        internal static OpenNfsCompoundRequest CreateIoAdviseV42Request(
            byte[] fileHandle,
            ulong offset,
            ulong count,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? hints)
        {
            if (count < 1UL)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested NFSv4.2 IO_ADVISE length must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "io-advise",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_IO_ADVISE,
                        EncodeV42Payload(
                            new IO_ADVISE4args
                            {
                                iaa_stateid = CreateAnonymousStateIdV42(),
                                iaa_offset = new offset4
                                {
                                    Value = offset,
                                },
                                iaa_count = new length4
                                {
                                    Value = count,
                                },
                                iaa_hints = CreateIoAdviceBitmap(hints),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateReadPlusV42Request(byte[] fileHandle, ulong offset, uint count)
        {
            if (count < 1U)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested NFSv4.2 READ_PLUS byte count must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "read-plus",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_READ_PLUS,
                        EncodeV42Payload(
                            new READ_PLUS4args
                            {
                                rpa_stateid = CreateAnonymousStateIdV42(),
                                rpa_offset = new offset4
                                {
                                    Value = offset,
                                },
                                rpa_count = new count4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateSeekV42Request(byte[] fileHandle, ulong offset, OpenNfsV42SeekTarget target)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "seek",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SEEK,
                        EncodeV42Payload(
                            new SEEK4args
                            {
                                sa_stateid = CreateAnonymousStateIdV42(),
                                sa_offset = new offset4
                                {
                                    Value = offset,
                                },
                                sa_what = CreateSeekTarget(target),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateAllocateV42Request(byte[] fileHandle, ulong offset, ulong length)
        {
            if (length < 1UL)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "The requested NFSv4.2 ALLOCATE length must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "allocate",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_ALLOCATE,
                        EncodeV42Payload(
                            new ALLOCATE4args
                            {
                                aa_stateid = CreateAnonymousStateIdV42(),
                                aa_offset = new offset4
                                {
                                    Value = offset,
                                },
                                aa_length = new length4
                                {
                                    Value = length,
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCopyV42Request(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            bool consecutive,
            bool synchronous)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "copy",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(sourceFileHandle, nameof(sourceFileHandle)),
                    CreateSaveFileHandleV42Operation(),
                    CreatePutFileHandleV42Operation(destinationFileHandle, nameof(destinationFileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_COPY,
                        EncodeV42Payload(
                            new COPY4args
                            {
                                ca_src_stateid = CreateAnonymousStateIdV42(),
                                ca_dst_stateid = CreateAnonymousStateIdV42(),
                                ca_src_offset = new offset4
                                {
                                    Value = sourceOffset,
                                },
                                ca_dst_offset = new offset4
                                {
                                    Value = destinationOffset,
                                },
                                ca_count = new length4
                                {
                                    Value = count,
                                },
                                ca_consecutive = consecutive,
                                ca_synchronous = synchronous,
                                ca_source_server = Array.Empty<netloc4>(),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCloneV42Request(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "clone",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(sourceFileHandle, nameof(sourceFileHandle)),
                    CreateSaveFileHandleV42Operation(),
                    CreatePutFileHandleV42Operation(destinationFileHandle, nameof(destinationFileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CLONE,
                        EncodeV42Payload(
                            new CLONE4args
                            {
                                cl_src_stateid = CreateAnonymousStateIdV42(),
                                cl_dst_stateid = CreateAnonymousStateIdV42(),
                                cl_src_offset = new offset4
                                {
                                    Value = sourceOffset,
                                },
                                cl_dst_offset = new offset4
                                {
                                    Value = destinationOffset,
                                },
                                cl_count = new length4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateDeallocateV42Request(byte[] fileHandle, ulong offset, ulong length)
        {
            if (length < 1UL)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "The requested NFSv4.2 DEALLOCATE length must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "deallocate",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleV42Operation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_DEALLOCATE,
                        EncodeV42Payload(
                            new DEALLOCATE4args
                            {
                                da_stateid = CreateAnonymousStateIdV42(),
                                da_offset = new offset4
                                {
                                    Value = offset,
                                },
                                da_length = new length4
                                {
                                    Value = length,
                                },
                            }.WriteTo)),
                });
        }
    }
}
