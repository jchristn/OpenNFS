namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V42.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    internal static class OpenNfsFileApiV42RequestSupport
    {
        internal static OpenNfsCompoundOperation CreatePutFileHandleV42Operation(byte[] fileHandle, string parameterName)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false);
            return new OpenNfsCompoundOperation(
                (uint)nfs_opnum4.OP_PUTFH,
                EncodeV42Payload(
                    new PUTFH4args
                    {
                        @object = new nfs_fh4
                        {
                            Value = safeFileHandle,
                        },
                    }.WriteTo));
        }

        internal static OpenNfsCompoundOperation CreateSaveFileHandleV42Operation()
        {
            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_SAVEFH, Array.Empty<byte>());
        }

        internal static stateid4 CreateAnonymousStateIdV42()
        {
            return new stateid4
            {
                seqid = 0U,
                other = new byte[12],
            };
        }

        internal static bitmap4 CreateIoAdviceBitmap(IReadOnlyList<OpenNfsV42IoAdviceHint>? hints)
        {
            if (hints is null || hints.Count == 0)
            {
                return new bitmap4
                {
                    Value = Array.Empty<uint>(),
                };
            }

            List<int> hintIds = new List<int>(hints.Count);
            for (int index = 0; index < hints.Count; index++)
            {
                int hintId = (int)hints[index];
                if (!hintIds.Contains(hintId))
                {
                    hintIds.Add(hintId);
                }
            }

            int highestHintId = -1;
            for (int index = 0; index < hintIds.Count; index++)
            {
                highestHintId = Math.Max(highestHintId, hintIds[index]);
            }

            uint[] words = new uint[(highestHintId / 32) + 1];
            for (int index = 0; index < hintIds.Count; index++)
            {
                int hintId = hintIds[index];
                int wordIndex = hintId / 32;
                int bitIndex = hintId % 32;
                words[wordIndex] |= 1U << bitIndex;
            }

            return new bitmap4
            {
                Value = words,
            };
        }

        internal static data_content4 CreateSeekTarget(OpenNfsV42SeekTarget target)
        {
            return target switch
            {
                OpenNfsV42SeekTarget.Data => data_content4.NFS4_CONTENT_DATA,
                OpenNfsV42SeekTarget.Hole => data_content4.NFS4_CONTENT_HOLE,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "The requested NFSv4.2 SEEK target is not supported."),
            };
        }
    }
}
