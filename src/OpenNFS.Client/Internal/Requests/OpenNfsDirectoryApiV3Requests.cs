namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    /// <summary>
    /// Encodes the grouped NFSv3 directory-oriented requests exposed by <see cref="OpenNFS.Client.Apis.DirectoryApis"/>.
    /// </summary>
    internal static class OpenNfsDirectoryApiV3Requests
    {
        internal static OpenNfsV3ProcedureRequest CreateLookupRequest(byte[] directoryHandle, string entryName)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            string safeEntryName = OpenNfsClientArgument.RequireEntryName(entryName, nameof(entryName));
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteString(safeEntryName);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsLookupProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateReadDirectoryRequest(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested directory byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteUInt64(cookie);
            writer.WriteFixedOpaque(safeCookieVerifier);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReaddirProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateReadDirectoryPlusRequest(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));

            if (directoryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(directoryCount), directoryCount, "The requested directory-entry byte count must be greater than zero.");
            }

            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The requested total reply byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteUInt64(cookie);
            writer.WriteFixedOpaque(safeCookieVerifier);
            writer.WriteUInt32(directoryCount);
            writer.WriteUInt32(maxCount);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReaddirPlusProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateFileRequest(byte[] directoryHandle, string entryName, bool failIfExists, uint mode)
        {
            CREATE3args arguments = new CREATE3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                how = new createhow3
                {
                    mode = failIfExists ? createmode3.GUARDED : createmode3.UNCHECKED,
                    obj_attributes = CreateModeAttributes(mode),
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsCreateProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateDirectoryRequest(byte[] directoryHandle, string entryName, uint mode)
        {
            MKDIR3args arguments = new MKDIR3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                attributes = CreateModeAttributes(mode),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsMkdirProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateRemoveFileRequest(byte[] directoryHandle, string entryName)
        {
            REMOVE3args arguments = new REMOVE3args
            {
                @object = CreateDirectoryOperationArguments(directoryHandle, entryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRemoveProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateRemoveDirectoryRequest(byte[] directoryHandle, string entryName)
        {
            RMDIR3args arguments = new RMDIR3args
            {
                @object = CreateDirectoryOperationArguments(directoryHandle, entryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRmdirProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateRenameRequest(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            RENAME3args arguments = new RENAME3args
            {
                from = CreateDirectoryOperationArguments(sourceDirectoryHandle, sourceEntryName),
                to = CreateDirectoryOperationArguments(destinationDirectoryHandle, destinationEntryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRenameProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateSymbolicLinkRequest(
            byte[] directoryHandle,
            string entryName,
            string targetPath)
        {
            string safeTargetPath = OpenNfsClientArgument.RequireText(targetPath, nameof(targetPath));
            SYMLINK3args arguments = new SYMLINK3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                symlink = new symlinkdata3
                {
                    symlink_attributes = CreateUnsetAttributes(),
                    symlink_data = new nfspath3
                    {
                        Value = safeTargetPath,
                    },
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsSymlinkProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateHardLinkRequest(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            LINK3args arguments = new LINK3args
            {
                file = CreateFileHandle(sourceFileHandle, nameof(sourceFileHandle)),
                link = CreateDirectoryOperationArguments(destinationDirectoryHandle, destinationEntryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsLinkProcedure, arguments.WriteTo);
        }

        private static diropargs3 CreateDirectoryOperationArguments(byte[] directoryHandle, string entryName)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            string safeEntryName = OpenNfsClientArgument.RequireEntryName(entryName, nameof(entryName));
            return new diropargs3
            {
                dir = new nfs_fh3
                {
                    data = safeDirectoryHandle,
                },
                name = new filename3
                {
                    Value = safeEntryName,
                },
            };
        }

        private static nfs_fh3 CreateFileHandle(byte[] fileHandle, string parameterName)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false);
            return new nfs_fh3
            {
                data = safeFileHandle,
            };
        }

        private static sattr3 CreateModeAttributes(uint mode)
        {
            sattr3 attributes = CreateUnsetAttributes();
            attributes.mode = new set_mode3 { set_it = true, mode = new mode3 { Value = new uint32 { Value = mode & 4095U } } };
            return attributes;
        }

        private static sattr3 CreateUnsetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }
    }
}
