namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV3ReplyDecodingSupport;

    internal static class OpenNfsV3DirectoryReplyDecoder
    {
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
    }
}
