namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V42.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV42ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV42ReplyValueReader;

    internal static class OpenNfsV42FileReplyDecoder
    {
        internal static OpenNfsV42IoAdviseResult ReadIoAdviseResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 IO_ADVISE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 IO_ADVISE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42IoAdviseResult(ReadTerminalStatus(result, overallStatus));
            }

            int operationIndex = ReadGroupedFileOperationIndex(result, "NFSv4.2 IO_ADVISE", nfs_opnum4.OP_IO_ADVISE);
            IO_ADVISE4res adviseResult = ReadExpectedOperation(result, operationIndex, nfs_opnum4.OP_IO_ADVISE, "NFSv4.2 IO_ADVISE").opio_advise
                ?? throw new InvalidDataException("The successful NFSv4.2 IO_ADVISE reply omitted the IO_ADVISE result arm.");
            IO_ADVISE4resok resok = adviseResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.2 IO_ADVISE reply omitted the IO_ADVISE success arm.");
            return new OpenNfsV42IoAdviseResult(overallStatus, MapIoAdviceHints(resok.ior_hints));
        }

        internal static OpenNfsV42ReadPlusResult ReadReadPlusResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 READ_PLUS");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 READ_PLUS"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42ReadPlusResult(ReadTerminalStatus(result, overallStatus));
            }

            int operationIndex = ReadGroupedFileOperationIndex(result, "NFSv4.2 READ_PLUS", nfs_opnum4.OP_READ_PLUS);
            READ_PLUS4res readPlusResult = ReadExpectedOperation(result, operationIndex, nfs_opnum4.OP_READ_PLUS, "NFSv4.2 READ_PLUS").opread_plus
                ?? throw new InvalidDataException("The successful NFSv4.2 READ_PLUS reply omitted the READ_PLUS result arm.");
            read_plus_res4 resok = readPlusResult.rp_resok4
                ?? throw new InvalidDataException("The successful NFSv4.2 READ_PLUS reply omitted the READ_PLUS success arm.");
            return new OpenNfsV42ReadPlusResult(overallStatus, resok.rpr_eof, MapReadPlusSegments(resok.rpr_contents));
        }

        internal static OpenNfsV42SeekResult ReadSeekResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 SEEK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 SEEK"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42SeekResult(ReadTerminalStatus(result, overallStatus));
            }

            int operationIndex = ReadGroupedFileOperationIndex(result, "NFSv4.2 SEEK", nfs_opnum4.OP_SEEK);
            SEEK4res seekResult = ReadExpectedOperation(result, operationIndex, nfs_opnum4.OP_SEEK, "NFSv4.2 SEEK").opseek
                ?? throw new InvalidDataException("The successful NFSv4.2 SEEK reply omitted the SEEK result arm.");
            seek_res4 resok = seekResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.2 SEEK reply omitted the SEEK success arm.");
            return new OpenNfsV42SeekResult(
                overallStatus,
                ReadRequiredUInt64(resok.sr_offset?.Value, "seek_res4.sr_offset"),
                resok.sr_eof);
        }

        internal static OpenNfsV42AllocateResult ReadAllocateResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 ALLOCATE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 ALLOCATE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42AllocateResult(ReadTerminalStatus(result, overallStatus));
            }

            _ = ReadGroupedFileOperationIndex(result, "NFSv4.2 ALLOCATE", nfs_opnum4.OP_ALLOCATE);
            return new OpenNfsV42AllocateResult(overallStatus);
        }

        internal static OpenNfsV42CopyResult ReadCopyResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 COPY");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 COPY"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42CopyResult(ReadTerminalStatus(result, overallStatus));
            }

            int operationIndex = ReadGroupedSavedHandleOperationIndex(result, "NFSv4.2 COPY", nfs_opnum4.OP_COPY);
            COPY4res copyResult = ReadExpectedOperation(result, operationIndex, nfs_opnum4.OP_COPY, "NFSv4.2 COPY").opcopy
                ?? throw new InvalidDataException("The successful NFSv4.2 COPY reply omitted the COPY result arm.");
            COPY4resok resok = copyResult.cr_resok4
                ?? throw new InvalidDataException("The successful NFSv4.2 COPY reply omitted the COPY success arm.");
            write_response4 response = resok.cr_response
                ?? throw new InvalidDataException("The successful NFSv4.2 COPY reply omitted the COPY write_response4 payload.");
            copy_requirements4 requirements = resok.cr_requirements
                ?? throw new InvalidDataException("The successful NFSv4.2 COPY reply omitted the COPY requirements payload.");
            return new OpenNfsV42CopyResult(
                overallStatus,
                ReadRequiredUInt64(response.wr_count?.Value, "write_response4.wr_count"),
                MapWriteStability(response.wr_committed),
                ReadRequiredFixedOpaque(response.wr_writeverf?.Value, "write_response4.wr_writeverf", expectedLength: 8),
                requirements.cr_consecutive,
                requirements.cr_synchronous);
        }

        internal static OpenNfsV42CloneResult ReadCloneResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 CLONE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 CLONE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42CloneResult(ReadTerminalStatus(result, overallStatus));
            }

            _ = ReadGroupedSavedHandleOperationIndex(result, "NFSv4.2 CLONE", nfs_opnum4.OP_CLONE);
            return new OpenNfsV42CloneResult(overallStatus);
        }

        internal static OpenNfsV42DeallocateResult ReadDeallocateResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.2 DEALLOCATE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.2 DEALLOCATE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV42DeallocateResult(ReadTerminalStatus(result, overallStatus));
            }

            _ = ReadGroupedFileOperationIndex(result, "NFSv4.2 DEALLOCATE", nfs_opnum4.OP_DEALLOCATE);
            return new OpenNfsV42DeallocateResult(overallStatus);
        }

        private static int ReadGroupedFileOperationIndex(COMPOUND4res result, string operationName, nfs_opnum4 expectedOperation)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");

            if (operations.Length == 2)
            {
                _ = ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 1, expectedOperation, operationName);
                return 1;
            }

            if (operations.Length == 3)
            {
                SEQUENCE4res sequenceResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SEQUENCE, operationName).opsequence
                    ?? throw new InvalidDataException("The successful " + operationName + " reply omitted the SEQUENCE result arm.");
                if (sequenceResult.sr_status != nfsstat4.NFS4_OK)
                {
                    throw new InvalidDataException("The successful " + operationName + " reply reported a non-successful SEQUENCE status.");
                }

                _ = ReadExpectedOperation(result, 1, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 2, expectedOperation, operationName);
                return 2;
            }

            throw new InvalidDataException(
                "The successful " + operationName + " reply reported "
                + operations.Length
                + " operation result(s) instead of the expected grouped shape.");
        }

        private static int ReadGroupedSavedHandleOperationIndex(COMPOUND4res result, string operationName, nfs_opnum4 expectedOperation)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");

            if (operations.Length == 4)
            {
                _ = ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SAVEFH, operationName);
                _ = ReadExpectedOperation(result, 2, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 3, expectedOperation, operationName);
                return 3;
            }

            if (operations.Length == 5)
            {
                SEQUENCE4res sequenceResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SEQUENCE, operationName).opsequence
                    ?? throw new InvalidDataException("The successful " + operationName + " reply omitted the SEQUENCE result arm.");
                if (sequenceResult.sr_status != nfsstat4.NFS4_OK)
                {
                    throw new InvalidDataException("The successful " + operationName + " reply reported a non-successful SEQUENCE status.");
                }

                _ = ReadExpectedOperation(result, 1, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 2, nfs_opnum4.OP_SAVEFH, operationName);
                _ = ReadExpectedOperation(result, 3, nfs_opnum4.OP_PUTFH, operationName);
                _ = ReadExpectedOperation(result, 4, expectedOperation, operationName);
                return 4;
            }

            throw new InvalidDataException(
                "The successful " + operationName + " reply reported "
                + operations.Length
                + " operation result(s) instead of the expected saved-handle grouped shape.");
        }

        private static OpenNfsWriteStability MapWriteStability(stable_how4? value)
        {
            stable_how4 stableValue = ReadRequiredEnum(value, "stable_how4");
            return stableValue switch
            {
                stable_how4.UNSTABLE4 => OpenNfsWriteStability.Unstable,
                stable_how4.DATA_SYNC4 => OpenNfsWriteStability.DataSync,
                stable_how4.FILE_SYNC4 => OpenNfsWriteStability.FileSync,
                _ => throw new InvalidDataException(
                    "The decoded NFSv4.2 stable_how4 value '" + stableValue.ToString() + "' is not supported."),
            };
        }

        private static IReadOnlyList<OpenNfsV42IoAdviceHint> MapIoAdviceHints(bitmap4? bitmap)
        {
            uint[] words = ReadBitmapWords(bitmap, "IO_ADVISE4resok.ior_hints");
            List<OpenNfsV42IoAdviceHint> hints = new List<OpenNfsV42IoAdviceHint>();
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                {
                    if ((word & (1U << bitIndex)) == 0U)
                    {
                        continue;
                    }

                    int hintId = (wordIndex * 32) + bitIndex;
                    if (Enum.IsDefined(typeof(OpenNfsV42IoAdviceHint), hintId))
                    {
                        hints.Add((OpenNfsV42IoAdviceHint)hintId);
                    }
                }
            }

            return hints;
        }

        private static IReadOnlyList<OpenNfsV42ReadPlusSegment> MapReadPlusSegments(read_plus_content[]? contents)
        {
            if (contents is null)
            {
                throw new InvalidDataException("The successful NFSv4.2 READ_PLUS reply omitted the content array.");
            }

            OpenNfsV42ReadPlusSegment[] segments = new OpenNfsV42ReadPlusSegment[contents.Length];
            for (int index = 0; index < contents.Length; index++)
            {
                read_plus_content content = contents[index]
                    ?? throw new InvalidDataException("The successful NFSv4.2 READ_PLUS reply contained a null content entry.");

                if (content.rpc_content == data_content4.NFS4_CONTENT_DATA)
                {
                    data4 dataSegment = content.rpc_data
                        ?? throw new InvalidDataException("The successful NFSv4.2 READ_PLUS data segment omitted its data arm.");
                    ReadOnlyMemory<byte> data = ReadRequiredOpaque(dataSegment.d_data, "data4.d_data", allowEmpty: true);
                    segments[index] = new OpenNfsV42ReadPlusSegment(
                        OpenNfsV42ReadPlusSegmentKind.Data,
                        ReadRequiredUInt64(dataSegment.d_offset?.Value, "data4.d_offset"),
                        (ulong)data.Length,
                        data);
                    continue;
                }

                if (content.rpc_content == data_content4.NFS4_CONTENT_HOLE)
                {
                    data_info4 holeSegment = content.rpc_hole
                        ?? throw new InvalidDataException("The successful NFSv4.2 READ_PLUS hole segment omitted its hole arm.");
                    segments[index] = new OpenNfsV42ReadPlusSegment(
                        OpenNfsV42ReadPlusSegmentKind.Hole,
                        ReadRequiredUInt64(holeSegment.di_offset?.Value, "data_info4.di_offset"),
                        ReadRequiredUInt64(holeSegment.di_length?.Value, "data_info4.di_length"));
                    continue;
                }

                throw new InvalidDataException("The successful NFSv4.2 READ_PLUS reply reported an unsupported content discriminator.");
            }

            return segments;
        }
    }
}
