namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs3ReadDirProcedureHandler : Nfs3ProcedureHandlerBase<READDIR3args, READDIR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3ReadDirProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR, READDIR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<READDIR3res> HandleCoreAsync(READDIR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.dir, cancellationToken).ConfigureAwait(false);

            if (directoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return new READDIR3res
                {
                    status = directoryResolution.Status,
                    resfail = new READDIR3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            if (directoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return new READDIR3res
                {
                    status = nfsstat3.NFS3ERR_NOTDIR,
                    resfail = new READDIR3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            NfsReadDirectoryResponse directoryResponse =
                await _server.Settings.FileSystem.ReadDirectoryAsync(
                    new NfsReadDirectoryRequest(
                        directoryResolution.Target!.SourcePath,
                        cancellationToken)).ConfigureAwait(false);

            IReadOnlyList<NfsDirectoryEntryInfo> directoryEntries = directoryResponse.Entries;
            byte[] currentVerifier = Nfs3DirectoryListingSupport.CreateCookieVerifier(
                directoryResolution.Target.SourcePath,
                directoryEntries);
            ulong requestedCookie = Nfs3DirectoryListingSupport.ReadCookie(arguments.cookie);
            byte[] suppliedVerifier = Nfs3DirectoryListingSupport.ReadCookieVerifier(arguments.cookieverf);

            if (!Nfs3DirectoryListingSupport.IsCookieValid(
                requestedCookie,
                suppliedVerifier,
                currentVerifier,
                directoryEntries.Count))
            {
                return new READDIR3res
                {
                    status = nfsstat3.NFS3ERR_BAD_COOKIE,
                    resfail = new READDIR3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            uint maximumCount = arguments.count?.Value?.Value ?? 0U;
            READDIR3res emptyResult = CreateSuccessResult(
                directoryResolution.PostOperationAttributes,
                currentVerifier,
                new List<entry3>(),
                eof: requestedCookie >= (ulong)directoryEntries.Count);

            if (maximumCount < Nfs3DirectoryListingSupport.MeasureReadDirectoryLength(emptyResult))
            {
                return new READDIR3res
                {
                    status = nfsstat3.NFS3ERR_TOOSMALL,
                    resfail = new READDIR3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            List<entry3> selectedEntries = new List<entry3>();
            bool eof = true;
            bool appendedEntry = false;
            int startIndex = checked((int)requestedCookie);

            for (int index = startIndex; index < directoryEntries.Count; index++)
            {
                NfsDirectoryEntryInfo directoryEntry = directoryEntries[index];
                NfsFileHandleTarget childTarget = new NfsFileHandleTarget(
                    directoryResolution.Target.ExportPath,
                    directoryEntry.PathInfo.Path);
                entry3 entry = new entry3
                {
                    fileid = Nfs3MetadataResolver.CreateFileId(childTarget),
                    name = new filename3
                    {
                        Value = directoryEntry.Name,
                    },
                    cookie = new cookie3
                    {
                        Value = Nfs3MetadataResolver.CreateUInt64((ulong)(index + 1)),
                    },
                };

                selectedEntries.Add(entry);
                READDIR3res candidateResult = CreateSuccessResult(
                    directoryResolution.PostOperationAttributes,
                    currentVerifier,
                    selectedEntries,
                    eof: index + 1 >= directoryEntries.Count);

                if (Nfs3DirectoryListingSupport.MeasureReadDirectoryLength(candidateResult) > maximumCount)
                {
                    selectedEntries.RemoveAt(selectedEntries.Count - 1);
                    eof = false;

                    if (!appendedEntry)
                    {
                        return new READDIR3res
                        {
                            status = nfsstat3.NFS3ERR_TOOSMALL,
                            resfail = new READDIR3resfail
                            {
                                dir_attributes = directoryResolution.PostOperationAttributes,
                            },
                        };
                    }

                    break;
                }

                appendedEntry = true;
            }

            if (requestedCookie >= (ulong)directoryEntries.Count)
            {
                eof = true;
            }

            return CreateSuccessResult(directoryResolution.PostOperationAttributes, currentVerifier, selectedEntries, eof);
        }

        protected override void WriteResult(READDIR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static READDIR3res CreateSuccessResult(
            post_op_attr directoryAttributes,
            byte[] cookieVerifier,
            IReadOnlyList<entry3> entries,
            bool eof)
        {
            return new READDIR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new READDIR3resok
                {
                    dir_attributes = directoryAttributes,
                    cookieverf = new cookieverf3
                    {
                        Value = cookieVerifier,
                    },
                    reply = new dirlist3
                    {
                        entries = Nfs3DirectoryListingSupport.BuildEntryList(entries),
                        eof = eof,
                    },
                },
            };
        }
    }
}
