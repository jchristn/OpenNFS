namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsV3ReplyDecodingSupport
    {
        internal static T DecodePayload<T>(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Func<XdrReader, T> readPayload)
        {
            ReadOnlyMemory<byte> procedurePayload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                encodedReply,
                operationName);
            XdrReader reader = new XdrReader(procedurePayload);
            T decodedPayload = readPayload(reader);
            reader.EnsureFullyConsumed();
            return decodedPayload;
        }

        internal static IReadOnlyList<OpenNfsV3DirectoryEntry> MapDirectoryEntries(entry3list? entries)
        {
            List<OpenNfsV3DirectoryEntry> mappedEntries = new List<OpenNfsV3DirectoryEntry>();
            entry3? current = entries?.Value;

            while (current is not null)
            {
                mappedEntries.Add(new OpenNfsV3DirectoryEntry(
                    ReadRequiredUInt64(current.fileid?.Value, "entry3.fileid"),
                    ReadRequiredText(current.name?.Value, "entry3.name", allowEmpty: false),
                    ReadRequiredUInt64(current.cookie?.Value, "entry3.cookie")));
                current = current.nextentry?.Value;
            }

            return mappedEntries.ToArray();
        }

        internal static IReadOnlyList<OpenNfsV3DirectoryPlusEntry> MapDirectoryPlusEntries(entryplus3list? entries)
        {
            List<OpenNfsV3DirectoryPlusEntry> mappedEntries = new List<OpenNfsV3DirectoryPlusEntry>();
            entryplus3? current = entries?.Value;

            while (current is not null)
            {
                mappedEntries.Add(new OpenNfsV3DirectoryPlusEntry(
                    ReadRequiredUInt64(current.fileid?.Value, "entryplus3.fileid"),
                    ReadRequiredText(current.name?.Value, "entryplus3.name", allowEmpty: false),
                    ReadRequiredUInt64(current.cookie?.Value, "entryplus3.cookie"),
                    MapPostOperationAttributes(current.name_attributes, "entryplus3.name_attributes"),
                    MapOptionalHandle(current.name_handle)));
                current = current.nextentry?.Value;
            }

            return mappedEntries.ToArray();
        }

        internal static OpenNfsV3WeakCacheConsistency? MapWeakCacheConsistency(wcc_data? value)
        {
            if (value is null)
            {
                return null;
            }

            return new OpenNfsV3WeakCacheConsistency(
                MapPreOperationAttributes(value.before),
                MapPostOperationAttributes(value.after, "wcc_data.after"));
        }

        internal static OpenNfsV3WeakCacheConsistencyAttributes? MapPreOperationAttributes(pre_op_attr? value)
        {
            if (value is null || !value.attributes_follow)
            {
                return null;
            }

            wcc_attr attributes = value.attributes
                ?? throw new InvalidDataException("The decoded pre_op_attr arm indicated attributes_follow but omitted the attributes payload.");
            return new OpenNfsV3WeakCacheConsistencyAttributes(
                ReadRequiredSize(attributes.size, "wcc_attr.size"),
                MapTime(attributes.mtime, "wcc_attr.mtime"),
                MapTime(attributes.ctime, "wcc_attr.ctime"));
        }

        internal static OpenNfsV3Attributes? MapPostOperationAttributes(post_op_attr? value, string fieldName)
        {
            if (value is null || !value.attributes_follow)
            {
                return null;
            }

            return MapRequiredAttributes(value.attributes, fieldName + ".attributes");
        }

        internal static OpenNfsV3Attributes MapRequiredAttributes(fattr3? value, string fieldName)
        {
            fattr3 attributes = value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            return new OpenNfsV3Attributes(
                MapFileType(ReadRequiredEnum(attributes.type, fieldName + ".type")),
                ReadRequiredUInt32(attributes.mode?.Value, fieldName + ".mode"),
                ReadRequiredUInt32(attributes.nlink, fieldName + ".nlink"),
                ReadRequiredUInt32(attributes.uid?.Value, fieldName + ".uid"),
                ReadRequiredUInt32(attributes.gid?.Value, fieldName + ".gid"),
                ReadRequiredSize(attributes.size, fieldName + ".size"),
                ReadRequiredSize(attributes.used, fieldName + ".used"),
                MapSpecData(attributes.rdev),
                ReadRequiredUInt64(attributes.fsid, fieldName + ".fsid"),
                ReadRequiredUInt64(attributes.fileid?.Value, fieldName + ".fileid"),
                MapTime(attributes.atime, fieldName + ".atime"),
                MapTime(attributes.mtime, fieldName + ".mtime"),
                MapTime(attributes.ctime, fieldName + ".ctime"));
        }

        internal static OpenNfsV3SpecData MapSpecData(specdata3? value)
        {
            specdata3 specData = value
                ?? throw new InvalidDataException("The decoded specdata3 field was required but missing.");
            return new OpenNfsV3SpecData(
                ReadRequiredUInt32(specData.specdata1, "specdata3.specdata1"),
                ReadRequiredUInt32(specData.specdata2, "specdata3.specdata2"));
        }

        internal static OpenNfsV3Time MapTime(nfstime3? value, string fieldName)
        {
            nfstime3 timeValue = value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            return new OpenNfsV3Time(
                ReadRequiredUInt32(timeValue.seconds, fieldName + ".seconds"),
                ReadRequiredUInt32(timeValue.nseconds, fieldName + ".nseconds"));
        }

        internal static OpenNfsWriteStability MapWriteStability(stable_how? value)
        {
            stable_how stability = ReadRequiredEnum(value, "stable_how");
            return stability switch
            {
                stable_how.UNSTABLE => OpenNfsWriteStability.Unstable,
                stable_how.DATA_SYNC => OpenNfsWriteStability.DataSync,
                stable_how.FILE_SYNC => OpenNfsWriteStability.FileSync,
                _ => throw new InvalidDataException("The decoded stable_how field reported unsupported stability '" + stability.ToString() + "'."),
            };
        }

        internal static OpenNfsV3Status MapStatus(nfsstat3 value)
        {
            return (OpenNfsV3Status)(int)value;
        }

        internal static OpenNfsV3FileType MapFileType(ftype3 value)
        {
            return (OpenNfsV3FileType)(int)value;
        }

        internal static ReadOnlyMemory<byte> MapOptionalHandle(post_op_fh3? value)
        {
            if (value is null || !value.handle_follows)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return ReadRequiredOpaque(value.handle?.data, "post_op_fh3.handle.data");
        }

        internal static T ReadRequiredEnum<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static nfsstat3 ReadRequiredStatus(nfsstat3? value, string operationName)
        {
            return ReadRequiredEnum(value, operationName + " status");
        }

        internal static uint ReadRequiredUInt32(uint32? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static ulong ReadRequiredUInt64(uint64? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static ulong ReadRequiredSize(size3? value, string fieldName)
        {
            uint64 nestedValue = value?.Value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            return nestedValue.Value;
        }

        internal static ReadOnlyMemory<byte> ReadRequiredOpaque(byte[]? value, string fieldName, bool allowEmpty = false)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && value.Length < 1)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return new ReadOnlyMemory<byte>(value.AsSpan().ToArray());
        }

        internal static ReadOnlyMemory<byte> ReadRequiredFixedOpaque(byte[]? value, string fieldName, int expectedLength)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (value.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "The decoded " + fieldName + " field must contain exactly " + expectedLength + " byte(s).");
            }

            return new ReadOnlyMemory<byte>(value.AsSpan().ToArray());
        }

        internal static string ReadRequiredText(string? value, string fieldName, bool allowEmpty)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return value;
        }
    }
}
