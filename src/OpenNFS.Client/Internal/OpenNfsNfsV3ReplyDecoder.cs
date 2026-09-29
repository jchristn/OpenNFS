namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsNfsV3ReplyDecoder
    {
        internal static OpenNfsV3GetAttributesResult ReadGetAttributesResult(ReadOnlyMemory<byte> encodedReply)
        {
            GETATTR3res result = DecodePayload(encodedReply, "NFSv3 GETATTR", GETATTR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 GETATTR"));
            if (status != OpenNfsV3Status.Ok)
            {
                return new OpenNfsV3GetAttributesResult(status);
            }

            GETATTR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 GETATTR result omitted the resok arm.");
            return new OpenNfsV3GetAttributesResult(
                status,
                MapRequiredAttributes(resok.obj_attributes, "GETATTR3res.resok.obj_attributes"));
        }

        internal static OpenNfsV3SetAttributesResult ReadSetAttributesResult(ReadOnlyMemory<byte> encodedReply)
        {
            SETATTR3res result = DecodePayload(encodedReply, "NFSv3 SETATTR", SETATTR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 SETATTR"));

            if (status != OpenNfsV3Status.Ok)
            {
                SETATTR3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 SETATTR result omitted the resfail arm.");
                return new OpenNfsV3SetAttributesResult(status, MapWeakCacheConsistency(resfail.obj_wcc));
            }

            SETATTR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 SETATTR result omitted the resok arm.");
            return new OpenNfsV3SetAttributesResult(status, MapWeakCacheConsistency(resok.obj_wcc));
        }

        internal static OpenNfsV3AccessResult ReadAccessResult(ReadOnlyMemory<byte> encodedReply)
        {
            ACCESS3res result = DecodePayload(encodedReply, "NFSv3 ACCESS", ACCESS3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 ACCESS"));

            if (status != OpenNfsV3Status.Ok)
            {
                ACCESS3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 ACCESS result omitted the resfail arm.");
                return new OpenNfsV3AccessResult(
                    status,
                    MapPostOperationAttributes(resfail.obj_attributes, "ACCESS3res.resfail.obj_attributes"));
            }

            ACCESS3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 ACCESS result omitted the resok arm.");
            return new OpenNfsV3AccessResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "ACCESS3res.resok.obj_attributes"),
                (OpenNfsV3AccessMask)ReadRequiredUInt32(resok.access, "ACCESS3res.resok.access"));
        }

        internal static OpenNfsV3CreatePathResult ReadCreatePathResult(ReadOnlyMemory<byte> encodedReply)
        {
            CREATE3res result = DecodePayload(encodedReply, "NFSv3 CREATE", CREATE3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 CREATE"));

            if (status != OpenNfsV3Status.Ok)
            {
                CREATE3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 CREATE result omitted the resfail arm.");
                return new OpenNfsV3CreatePathResult(
                    status,
                    directoryWeakCacheConsistency: MapWeakCacheConsistency(resfail.dir_wcc));
            }

            CREATE3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 CREATE result omitted the resok arm.");
            return new OpenNfsV3CreatePathResult(
                status,
                MapOptionalHandle(resok.obj),
                MapPostOperationAttributes(resok.obj_attributes, "CREATE3res.resok.obj_attributes"),
                MapWeakCacheConsistency(resok.dir_wcc));
        }

        internal static OpenNfsV3CreatePathResult ReadCreateDirectoryResult(ReadOnlyMemory<byte> encodedReply)
        {
            MKDIR3res result = DecodePayload(encodedReply, "NFSv3 MKDIR", MKDIR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 MKDIR"));

            if (status != OpenNfsV3Status.Ok)
            {
                MKDIR3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 MKDIR result omitted the resfail arm.");
                return new OpenNfsV3CreatePathResult(
                    status,
                    directoryWeakCacheConsistency: MapWeakCacheConsistency(resfail.dir_wcc));
            }

            MKDIR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 MKDIR result omitted the resok arm.");
            return new OpenNfsV3CreatePathResult(
                status,
                MapOptionalHandle(resok.obj),
                MapPostOperationAttributes(resok.obj_attributes, "MKDIR3res.resok.obj_attributes"),
                MapWeakCacheConsistency(resok.dir_wcc));
        }

        internal static OpenNfsV3LookupResult ReadLookupResult(ReadOnlyMemory<byte> encodedReply)
        {
            LOOKUP3res result = DecodePayload(encodedReply, "NFSv3 LOOKUP", LOOKUP3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 LOOKUP"));

            if (status != OpenNfsV3Status.Ok)
            {
                LOOKUP3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 LOOKUP result omitted the resfail arm.");
                return new OpenNfsV3LookupResult(
                    status,
                    directoryAttributes: MapPostOperationAttributes(resfail.dir_attributes, "LOOKUP3res.resfail.dir_attributes"));
            }

            LOOKUP3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 LOOKUP result omitted the resok arm.");
            return new OpenNfsV3LookupResult(
                status,
                ReadRequiredOpaque(resok.@object?.data, "LOOKUP3res.resok.object.data"),
                MapPostOperationAttributes(resok.obj_attributes, "LOOKUP3res.resok.obj_attributes"),
                MapPostOperationAttributes(resok.dir_attributes, "LOOKUP3res.resok.dir_attributes"));
        }

        internal static OpenNfsV3LinkResult ReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            LINK3res result = DecodePayload(encodedReply, "NFSv3 LINK", LINK3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 LINK"));

            if (status != OpenNfsV3Status.Ok)
            {
                LINK3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 LINK result omitted the resfail arm.");
                return new OpenNfsV3LinkResult(
                    status,
                    MapPostOperationAttributes(resfail.file_attributes, "LINK3res.resfail.file_attributes"),
                    MapWeakCacheConsistency(resfail.linkdir_wcc));
            }

            LINK3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 LINK result omitted the resok arm.");
            return new OpenNfsV3LinkResult(
                status,
                MapPostOperationAttributes(resok.file_attributes, "LINK3res.resok.file_attributes"),
                MapWeakCacheConsistency(resok.linkdir_wcc));
        }

        internal static OpenNfsV3ReadResult ReadReadResult(ReadOnlyMemory<byte> encodedReply)
        {
            READ3res result = DecodePayload(encodedReply, "NFSv3 READ", READ3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 READ"));

            if (status != OpenNfsV3Status.Ok)
            {
                READ3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 READ result omitted the resfail arm.");
                return new OpenNfsV3ReadResult(
                    status,
                    fileAttributes: MapPostOperationAttributes(resfail.file_attributes, "READ3res.resfail.file_attributes"));
            }

            READ3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 READ result omitted the resok arm.");
            return new OpenNfsV3ReadResult(
                status,
                MapPostOperationAttributes(resok.file_attributes, "READ3res.resok.file_attributes"),
                ReadRequiredUInt32(resok.count?.Value, "READ3res.resok.count"),
                resok.eof,
                ReadRequiredOpaque(resok.data, "READ3res.resok.data", allowEmpty: true));
        }

        internal static OpenNfsV3ReadDirectoryResult ReadReadDirectoryResult(ReadOnlyMemory<byte> encodedReply)
        {
            READDIR3res result = DecodePayload(encodedReply, "NFSv3 READDIR", READDIR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 READDIR"));

            if (status != OpenNfsV3Status.Ok)
            {
                READDIR3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 READDIR result omitted the resfail arm.");
                return new OpenNfsV3ReadDirectoryResult(
                    status,
                    directoryAttributes: MapPostOperationAttributes(resfail.dir_attributes, "READDIR3res.resfail.dir_attributes"));
            }

            READDIR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 READDIR result omitted the resok arm.");
            dirlist3 reply = resok.reply
                ?? throw new InvalidDataException("The successful NFSv3 READDIR result omitted the reply payload.");
            return new OpenNfsV3ReadDirectoryResult(
                status,
                MapPostOperationAttributes(resok.dir_attributes, "READDIR3res.resok.dir_attributes"),
                ReadRequiredFixedOpaque(resok.cookieverf?.Value, "READDIR3res.resok.cookieverf", 8),
                MapDirectoryEntries(reply.entries),
                reply.eof);
        }

        internal static OpenNfsV3RenameResult ReadRenameResult(ReadOnlyMemory<byte> encodedReply)
        {
            RENAME3res result = DecodePayload(encodedReply, "NFSv3 RENAME", RENAME3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 RENAME"));

            if (status != OpenNfsV3Status.Ok)
            {
                RENAME3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 RENAME result omitted the resfail arm.");
                return new OpenNfsV3RenameResult(
                    status,
                    MapWeakCacheConsistency(resfail.fromdir_wcc),
                    MapWeakCacheConsistency(resfail.todir_wcc));
            }

            RENAME3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 RENAME result omitted the resok arm.");
            return new OpenNfsV3RenameResult(
                status,
                MapWeakCacheConsistency(resok.fromdir_wcc),
                MapWeakCacheConsistency(resok.todir_wcc));
        }

        internal static OpenNfsV3DirectoryMutationResult ReadRemoveFileResult(ReadOnlyMemory<byte> encodedReply)
        {
            REMOVE3res result = DecodePayload(encodedReply, "NFSv3 REMOVE", REMOVE3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 REMOVE"));

            if (status != OpenNfsV3Status.Ok)
            {
                REMOVE3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 REMOVE result omitted the resfail arm.");
                return new OpenNfsV3DirectoryMutationResult(
                    status,
                    MapWeakCacheConsistency(resfail.dir_wcc));
            }

            REMOVE3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 REMOVE result omitted the resok arm.");
            return new OpenNfsV3DirectoryMutationResult(
                status,
                MapWeakCacheConsistency(resok.dir_wcc));
        }

        internal static OpenNfsV3DirectoryMutationResult ReadRemoveDirectoryResult(ReadOnlyMemory<byte> encodedReply)
        {
            RMDIR3res result = DecodePayload(encodedReply, "NFSv3 RMDIR", RMDIR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 RMDIR"));

            if (status != OpenNfsV3Status.Ok)
            {
                RMDIR3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 RMDIR result omitted the resfail arm.");
                return new OpenNfsV3DirectoryMutationResult(
                    status,
                    MapWeakCacheConsistency(resfail.dir_wcc));
            }

            RMDIR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 RMDIR result omitted the resok arm.");
            return new OpenNfsV3DirectoryMutationResult(
                status,
                MapWeakCacheConsistency(resok.dir_wcc));
        }

        internal static OpenNfsV3ReadDirectoryPlusResult ReadReadDirectoryPlusResult(ReadOnlyMemory<byte> encodedReply)
        {
            READDIRPLUS3res result = DecodePayload(encodedReply, "NFSv3 READDIRPLUS", READDIRPLUS3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 READDIRPLUS"));

            if (status != OpenNfsV3Status.Ok)
            {
                READDIRPLUS3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 READDIRPLUS result omitted the resfail arm.");
                return new OpenNfsV3ReadDirectoryPlusResult(
                    status,
                    directoryAttributes: MapPostOperationAttributes(resfail.dir_attributes, "READDIRPLUS3res.resfail.dir_attributes"));
            }

            READDIRPLUS3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 READDIRPLUS result omitted the resok arm.");
            dirlistplus3 reply = resok.reply
                ?? throw new InvalidDataException("The successful NFSv3 READDIRPLUS result omitted the reply payload.");
            return new OpenNfsV3ReadDirectoryPlusResult(
                status,
                MapPostOperationAttributes(resok.dir_attributes, "READDIRPLUS3res.resok.dir_attributes"),
                ReadRequiredFixedOpaque(resok.cookieverf?.Value, "READDIRPLUS3res.resok.cookieverf", 8),
                MapDirectoryPlusEntries(reply.entries),
                reply.eof);
        }

        internal static OpenNfsV3ReadLinkResult ReadReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            READLINK3res result = DecodePayload(encodedReply, "NFSv3 READLINK", READLINK3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 READLINK"));

            if (status != OpenNfsV3Status.Ok)
            {
                READLINK3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 READLINK result omitted the resfail arm.");
                return new OpenNfsV3ReadLinkResult(
                    status,
                    symbolicLinkAttributes: MapPostOperationAttributes(resfail.symlink_attributes, "READLINK3res.resfail.symlink_attributes"));
            }

            READLINK3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 READLINK result omitted the resok arm.");
            return new OpenNfsV3ReadLinkResult(
                status,
                MapPostOperationAttributes(resok.symlink_attributes, "READLINK3res.resok.symlink_attributes"),
                ReadRequiredText(resok.data?.Value, "READLINK3res.resok.data", allowEmpty: true));
        }

        internal static OpenNfsV3CreatePathResult ReadCreateSymbolicLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            SYMLINK3res result = DecodePayload(encodedReply, "NFSv3 SYMLINK", SYMLINK3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 SYMLINK"));

            if (status != OpenNfsV3Status.Ok)
            {
                SYMLINK3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 SYMLINK result omitted the resfail arm.");
                return new OpenNfsV3CreatePathResult(
                    status,
                    directoryWeakCacheConsistency: MapWeakCacheConsistency(resfail.dir_wcc));
            }

            SYMLINK3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 SYMLINK result omitted the resok arm.");
            return new OpenNfsV3CreatePathResult(
                status,
                MapOptionalHandle(resok.obj),
                MapPostOperationAttributes(resok.obj_attributes, "SYMLINK3res.resok.obj_attributes"),
                MapWeakCacheConsistency(resok.dir_wcc));
        }

        internal static OpenNfsV3FileSystemStatusResult ReadFileSystemStatusResult(ReadOnlyMemory<byte> encodedReply)
        {
            FSSTAT3res result = DecodePayload(encodedReply, "NFSv3 FSSTAT", FSSTAT3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 FSSTAT"));

            if (status != OpenNfsV3Status.Ok)
            {
                FSSTAT3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 FSSTAT result omitted the resfail arm.");
                return new OpenNfsV3FileSystemStatusResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "FSSTAT3res.resfail.obj_attributes"));
            }

            FSSTAT3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 FSSTAT result omitted the resok arm.");
            return new OpenNfsV3FileSystemStatusResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "FSSTAT3res.resok.obj_attributes"),
                ReadRequiredSize(resok.tbytes, "FSSTAT3res.resok.tbytes"),
                ReadRequiredSize(resok.fbytes, "FSSTAT3res.resok.fbytes"),
                ReadRequiredSize(resok.abytes, "FSSTAT3res.resok.abytes"),
                ReadRequiredSize(resok.tfiles, "FSSTAT3res.resok.tfiles"),
                ReadRequiredSize(resok.ffiles, "FSSTAT3res.resok.ffiles"),
                ReadRequiredSize(resok.afiles, "FSSTAT3res.resok.afiles"),
                ReadRequiredUInt32(resok.invarsec, "FSSTAT3res.resok.invarsec"));
        }

        internal static OpenNfsV3FileSystemInfoResult ReadFileSystemInfoResult(ReadOnlyMemory<byte> encodedReply)
        {
            FSINFO3res result = DecodePayload(encodedReply, "NFSv3 FSINFO", FSINFO3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 FSINFO"));

            if (status != OpenNfsV3Status.Ok)
            {
                FSINFO3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 FSINFO result omitted the resfail arm.");
                return new OpenNfsV3FileSystemInfoResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "FSINFO3res.resfail.obj_attributes"));
            }

            FSINFO3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 FSINFO result omitted the resok arm.");
            return new OpenNfsV3FileSystemInfoResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "FSINFO3res.resok.obj_attributes"),
                ReadRequiredUInt32(resok.rtmax, "FSINFO3res.resok.rtmax"),
                ReadRequiredUInt32(resok.rtpref, "FSINFO3res.resok.rtpref"),
                ReadRequiredUInt32(resok.rtmult, "FSINFO3res.resok.rtmult"),
                ReadRequiredUInt32(resok.wtmax, "FSINFO3res.resok.wtmax"),
                ReadRequiredUInt32(resok.wtpref, "FSINFO3res.resok.wtpref"),
                ReadRequiredUInt32(resok.wtmult, "FSINFO3res.resok.wtmult"),
                ReadRequiredUInt32(resok.dtpref, "FSINFO3res.resok.dtpref"),
                ReadRequiredSize(resok.maxfilesize, "FSINFO3res.resok.maxfilesize"),
                MapTime(resok.time_delta, "FSINFO3res.resok.time_delta"),
                (OpenNfsV3FileSystemProperties)ReadRequiredUInt32(resok.properties, "FSINFO3res.resok.properties"));
        }

        internal static OpenNfsV3PathConfigurationResult ReadPathConfigurationResult(ReadOnlyMemory<byte> encodedReply)
        {
            PATHCONF3res result = DecodePayload(encodedReply, "NFSv3 PATHCONF", PATHCONF3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 PATHCONF"));

            if (status != OpenNfsV3Status.Ok)
            {
                PATHCONF3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 PATHCONF result omitted the resfail arm.");
                return new OpenNfsV3PathConfigurationResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "PATHCONF3res.resfail.obj_attributes"));
            }

            PATHCONF3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 PATHCONF result omitted the resok arm.");
            return new OpenNfsV3PathConfigurationResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "PATHCONF3res.resok.obj_attributes"),
                ReadRequiredUInt32(resok.linkmax, "PATHCONF3res.resok.linkmax"),
                ReadRequiredUInt32(resok.name_max, "PATHCONF3res.resok.name_max"),
                resok.no_trunc,
                resok.chown_restricted,
                resok.case_insensitive,
                resok.case_preserving);
        }

        internal static OpenNfsV3WriteResult ReadWriteResult(ReadOnlyMemory<byte> encodedReply)
        {
            WRITE3res result = DecodePayload(encodedReply, "NFSv3 WRITE", WRITE3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 WRITE"));

            if (status != OpenNfsV3Status.Ok)
            {
                WRITE3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 WRITE result omitted the resfail arm.");
                return new OpenNfsV3WriteResult(
                    status,
                    fileWeakCacheConsistency: MapWeakCacheConsistency(resfail.file_wcc));
            }

            WRITE3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 WRITE result omitted the resok arm.");
            return new OpenNfsV3WriteResult(
                status,
                MapWeakCacheConsistency(resok.file_wcc),
                ReadRequiredUInt32(resok.count?.Value, "WRITE3res.resok.count"),
                MapWriteStability(resok.committed),
                ReadRequiredFixedOpaque(resok.verf?.Value, "WRITE3res.resok.verf", 8));
        }

        internal static OpenNfsV3CommitResult ReadCommitResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMMIT3res result = DecodePayload(encodedReply, "NFSv3 COMMIT", COMMIT3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 COMMIT"));

            if (status != OpenNfsV3Status.Ok)
            {
                COMMIT3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 COMMIT result omitted the resfail arm.");
                return new OpenNfsV3CommitResult(
                    status,
                    fileWeakCacheConsistency: MapWeakCacheConsistency(resfail.file_wcc));
            }

            COMMIT3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 COMMIT result omitted the resok arm.");
            return new OpenNfsV3CommitResult(
                status,
                MapWeakCacheConsistency(resok.file_wcc),
                ReadRequiredFixedOpaque(resok.verf?.Value, "COMMIT3res.resok.verf", 8));
        }

        private static T DecodePayload<T>(
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

        private static IReadOnlyList<OpenNfsV3DirectoryEntry> MapDirectoryEntries(entry3list? entries)
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

        private static IReadOnlyList<OpenNfsV3DirectoryPlusEntry> MapDirectoryPlusEntries(entryplus3list? entries)
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

        private static OpenNfsV3WeakCacheConsistency? MapWeakCacheConsistency(wcc_data? value)
        {
            if (value is null)
            {
                return null;
            }

            return new OpenNfsV3WeakCacheConsistency(
                MapPreOperationAttributes(value.before),
                MapPostOperationAttributes(value.after, "wcc_data.after"));
        }

        private static OpenNfsV3WeakCacheConsistencyAttributes? MapPreOperationAttributes(pre_op_attr? value)
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

        private static OpenNfsV3Attributes? MapPostOperationAttributes(post_op_attr? value, string fieldName)
        {
            if (value is null || !value.attributes_follow)
            {
                return null;
            }

            return MapRequiredAttributes(value.attributes, fieldName + ".attributes");
        }

        private static OpenNfsV3Attributes MapRequiredAttributes(fattr3? value, string fieldName)
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

        private static OpenNfsV3SpecData MapSpecData(specdata3? value)
        {
            specdata3 specData = value
                ?? throw new InvalidDataException("The decoded specdata3 field was required but missing.");
            return new OpenNfsV3SpecData(
                ReadRequiredUInt32(specData.specdata1, "specdata3.specdata1"),
                ReadRequiredUInt32(specData.specdata2, "specdata3.specdata2"));
        }

        private static OpenNfsV3Time MapTime(nfstime3? value, string fieldName)
        {
            nfstime3 timeValue = value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            return new OpenNfsV3Time(
                ReadRequiredUInt32(timeValue.seconds, fieldName + ".seconds"),
                ReadRequiredUInt32(timeValue.nseconds, fieldName + ".nseconds"));
        }

        private static OpenNfsWriteStability MapWriteStability(stable_how? value)
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

        private static OpenNfsV3Status MapStatus(nfsstat3 value)
        {
            return (OpenNfsV3Status)(int)value;
        }

        private static OpenNfsV3FileType MapFileType(ftype3 value)
        {
            return (OpenNfsV3FileType)(int)value;
        }

        private static ReadOnlyMemory<byte> MapOptionalHandle(post_op_fh3? value)
        {
            if (value is null || !value.handle_follows)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return ReadRequiredOpaque(value.handle?.data, "post_op_fh3.handle.data");
        }

        private static T ReadRequiredEnum<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static nfsstat3 ReadRequiredStatus(nfsstat3? value, string operationName)
        {
            return ReadRequiredEnum(value, operationName + " status");
        }

        private static uint ReadRequiredUInt32(uint32? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static ulong ReadRequiredUInt64(uint64? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static ulong ReadRequiredSize(size3? value, string fieldName)
        {
            uint64 nestedValue = value?.Value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            return nestedValue.Value;
        }

        private static ReadOnlyMemory<byte> ReadRequiredOpaque(byte[]? value, string fieldName, bool allowEmpty = false)
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

        private static ReadOnlyMemory<byte> ReadRequiredFixedOpaque(byte[]? value, string fieldName, int expectedLength)
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

        private static string ReadRequiredText(string? value, string fieldName, bool allowEmpty)
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
