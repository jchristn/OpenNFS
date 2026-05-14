namespace OpenNFS.Client.Internal
{
    using System;
    using System.Text;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    /// <summary>
    /// Encodes the grouped NFSv4.0 directory-oriented requests exposed by <see cref="OpenNFS.Client.Apis.DirectoryApis"/>.
    /// </summary>
    internal static class OpenNfsDirectoryApiV40Requests
    {
        internal static OpenNfsCompoundRequest CreateGetRootV40Request()
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "putrootfh",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
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

        internal static OpenNfsCompoundRequest CreateLookupV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lookup",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOOKUP,
                        EncodeV40Payload(
                            new LOOKUP4args
                            {
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
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

        internal static OpenNfsCompoundRequest CreateLookupParentV40Request(byte[] childHandle)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lookupp",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(childHandle, nameof(childHandle)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_LOOKUPP, Array.Empty<byte>()),
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

        internal static OpenNfsCompoundRequest CreateSecurityInfoV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "secinfo",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SECINFO,
                        EncodeV40Payload(
                            new SECINFO4args
                            {
                                name = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateCreateDirectoryV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "create-dir",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CREATE,
                        EncodeV40Payload(
                            new CREATE4args
                            {
                                objtype = new createtype4
                                {
                                    type = nfs_ftype4.NF4DIR,
                                },
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                                createattrs = CreateEmptyV40Attributes(),
                            }.WriteTo)),
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

        internal static OpenNfsCompoundRequest CreateCreateSymbolicLinkV40Request(
            byte[] directoryHandle,
            string entryName,
            string targetPath)
        {
            string safeTargetPath = OpenNfsClientArgument.RequireText(targetPath, nameof(targetPath));
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "create-symlink",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CREATE,
                        EncodeV40Payload(
                            new CREATE4args
                            {
                                objtype = new createtype4
                                {
                                    type = nfs_ftype4.NF4LNK,
                                    linkdata = new linktext4
                                    {
                                        Value = Encoding.UTF8.GetBytes(safeTargetPath),
                                    },
                                },
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                                createattrs = CreateEmptyV40Attributes(),
                            }.WriteTo)),
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

        internal static OpenNfsCompoundRequest CreateRemoveEntryV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "remove",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_REMOVE,
                        EncodeV40Payload(
                            new REMOVE4args
                            {
                                target = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateRenameV40Request(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] targetDirectoryHandle,
            string targetEntryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "rename",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(sourceDirectoryHandle, nameof(sourceDirectoryHandle)),
                    CreateSaveFileHandleOperation(),
                    CreatePutFileHandleOperation(targetDirectoryHandle, nameof(targetDirectoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_RENAME,
                        EncodeV40Payload(
                            new RENAME4args
                            {
                                oldname = CreatePathComponent(sourceEntryName, nameof(sourceEntryName)),
                                newname = CreatePathComponent(targetEntryName, nameof(targetEntryName)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateHardLinkV40Request(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "link",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(sourceFileHandle, nameof(sourceFileHandle)),
                    CreateSaveFileHandleOperation(),
                    CreatePutFileHandleOperation(destinationDirectoryHandle, nameof(destinationDirectoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LINK,
                        EncodeV40Payload(
                            new LINK4args
                            {
                                newname = CreatePathComponent(destinationEntryName, nameof(destinationEntryName)),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateReadDirectoryV40Request(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint maxCount)
        {
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The requested NFSv4 READDIR reply byte count must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "readdir",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_READDIR,
                        EncodeV40Payload(
                            new READDIR4args
                            {
                                cookie = new nfs_cookie4
                                {
                                    Value = cookie,
                                },
                                cookieverf = new verifier4
                                {
                                    Value = safeCookieVerifier,
                                },
                                dircount = new count4
                                {
                                    Value = maxCount,
                                },
                                maxcount = new count4
                                {
                                    Value = maxCount,
                                },
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }
    }
}
