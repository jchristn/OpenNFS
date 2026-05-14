namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Shared client-side request encoding helpers used by the grouped API surfaces.
    /// Centralizing these helpers keeps the wire-format validation rules in one place.
    /// </summary>
    internal static class OpenNfsApiEncoding
    {
        internal static seqid4 CreateSequenceId(uint sequenceId, string parameterName)
        {
            if (sequenceId == 0U)
            {
                throw new ArgumentOutOfRangeException(parameterName, sequenceId, "The requested NFSv4 sequence id must be greater than zero.");
            }

            return new seqid4
            {
                Value = sequenceId,
            };
        }

        internal static stateid4 CreateStateId(OpenNfsV40StateId stateId, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(stateId);
            return new stateid4
            {
                seqid = stateId.SequenceId,
                other = OpenNfsClientArgument.RequireFixedBytes(stateId.Other.ToArray(), expectedLength: 12, parameterName),
            };
        }

        internal static bitmap4 CreateDefaultV40AttributeRequest()
        {
            return new bitmap4
            {
                Value = new[]
                {
                    (1U << (int)Nfs40Constants.FATTR4_TYPE)
                    | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                    | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                    | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                },
            };
        }

        internal static bitmap4 CreateV40AttributeRequest(IReadOnlyList<OpenNfsV40AttributeKind> requestedAttributes)
        {
            ArgumentNullException.ThrowIfNull(requestedAttributes);
            if (requestedAttributes.Count < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedAttributes),
                    requestedAttributes.Count,
                    "The requested NFSv4 GETATTR attribute set must contain at least one attribute.");
            }

            List<int> attributeIds = new List<int>(requestedAttributes.Count);
            for (int index = 0; index < requestedAttributes.Count; index++)
            {
                int attributeId = (int)requestedAttributes[index];
                if (!attributeIds.Contains(attributeId))
                {
                    attributeIds.Add(attributeId);
                }
            }

            int highestAttributeId = -1;
            for (int index = 0; index < attributeIds.Count; index++)
            {
                highestAttributeId = Math.Max(highestAttributeId, attributeIds[index]);
            }

            uint[] words = new uint[(highestAttributeId / 32) + 1];
            for (int index = 0; index < attributeIds.Count; index++)
            {
                int attributeId = attributeIds[index];
                int wordIndex = attributeId / 32;
                int bitIndex = attributeId % 32;
                words[wordIndex] |= 1U << bitIndex;
            }

            return new bitmap4
            {
                Value = words,
            };
        }

        internal static fattr4 CreateEmptyV40Attributes()
        {
            return new fattr4
            {
                attrmask = new bitmap4
                {
                    Value = Array.Empty<uint>(),
                },
                attr_vals = new attrlist4
                {
                    Value = Array.Empty<byte>(),
                },
            };
        }

        internal static OpenNfsCompoundOperation CreatePutFileHandleOperation(byte[] fileHandle, string parameterName)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false);
            return new OpenNfsCompoundOperation(
                (uint)nfs_opnum4.OP_PUTFH,
                EncodeV40Payload(
                    new PUTFH4args
                    {
                        @object = new nfs_fh4
                        {
                            Value = safeFileHandle,
                        },
                    }.WriteTo));
        }

        internal static OpenNfsCompoundOperation CreateSaveFileHandleOperation()
        {
            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_SAVEFH, Array.Empty<byte>());
        }

        internal static component4 CreatePathComponent(string entryName, string parameterName)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes(OpenNfsClientArgument.RequireText(entryName, parameterName)),
                    },
                },
            };
        }

        internal static byte[] EncodeV40Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        internal static byte[] EncodeV42Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        internal static OpenNfsV3ProcedureRequest CreateEncodedRequest(
            uint procedureNumber,
            Action<XdrWriter> writePayload,
            ulong programNumber = 100003,
            ulong versionNumber = 3)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray(),
                programNumber: programNumber,
                versionNumber: versionNumber);
        }
    }
}
