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

    internal sealed class Nfs3ReadDirPlusProcedureHandler : Nfs3ProcedureHandlerBase<READDIRPLUS3args, READDIRPLUS3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3ReadDirPlusProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIRPLUS, READDIRPLUS3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<READDIRPLUS3res> HandleCoreAsync(READDIRPLUS3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.dir, cancellationToken).ConfigureAwait(false);

            if (directoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return new READDIRPLUS3res
                {
                    status = directoryResolution.Status,
                    resfail = new READDIRPLUS3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            if (directoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return new READDIRPLUS3res
                {
                    status = nfsstat3.NFS3ERR_NOTDIR,
                    resfail = new READDIRPLUS3resfail
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
                return new READDIRPLUS3res
                {
                    status = nfsstat3.NFS3ERR_BAD_COOKIE,
                    resfail = new READDIRPLUS3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            uint directoryInformationBudget = arguments.dircount?.Value?.Value ?? 0U;
            uint maximumCount = arguments.maxcount?.Value?.Value ?? 0U;
            READDIRPLUS3res emptyResult = CreateSuccessResult(
                directoryResolution.PostOperationAttributes,
                currentVerifier,
                new List<entryplus3>(),
                eof: requestedCookie >= (ulong)directoryEntries.Count);

            if (directoryInformationBudget < Nfs3DirectoryListingSupport.MeasureDirectoryInformationLength(
                    new dirlist3
                    {
                        entries = new entry3list(),
                        eof = requestedCookie >= (ulong)directoryEntries.Count,
                    })
                || maximumCount < Nfs3DirectoryListingSupport.MeasureReadDirectoryPlusLength(emptyResult))
            {
                return new READDIRPLUS3res
                {
                    status = nfsstat3.NFS3ERR_TOOSMALL,
                    resfail = new READDIRPLUS3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            List<entryplus3> selectedEntries = new List<entryplus3>();
            List<entry3> selectedDirectoryInfoEntries = new List<entry3>();
            bool eof = true;
            bool appendedEntry = false;
            int startIndex = checked((int)requestedCookie);

            for (int index = startIndex; index < directoryEntries.Count; index++)
            {
                NfsDirectoryEntryInfo directoryEntry = directoryEntries[index];
                NfsFileHandleTarget childTarget = new NfsFileHandleTarget(
                    directoryResolution.Target.ExportPath,
                    directoryEntry.PathInfo.Path);
                entry3 directoryInformationEntry = new entry3
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

                selectedDirectoryInfoEntries.Add(directoryInformationEntry);

                post_op_attr nameAttributes = new post_op_attr
                {
                    attributes_follow = false,
                };
                post_op_fh3 nameHandle = new post_op_fh3
                {
                    handle_follows = false,
                };

                if (directoryEntry.PathInfo.Kind != NfsPathKind.Other)
                {
                    fattr3 attributes = Nfs3MetadataResolver.CreateAttributes(childTarget, directoryEntry.PathInfo);
                    nameAttributes = new post_op_attr
                    {
                        attributes_follow = true,
                        attributes = attributes,
                    };

                    NfsFileHandle childFileHandle =
                        await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
                    nameHandle = new post_op_fh3
                    {
                        handle_follows = true,
                        handle = new nfs_fh3
                        {
                            data = childFileHandle.ToArray(),
                        },
                    };
                }

                entryplus3 entry = new entryplus3
                {
                    fileid = directoryInformationEntry.fileid,
                    name = directoryInformationEntry.name,
                    cookie = directoryInformationEntry.cookie,
                    name_attributes = nameAttributes,
                    name_handle = nameHandle,
                };

                selectedEntries.Add(entry);

                dirlist3 directoryInformationListing = new dirlist3
                {
                    entries = Nfs3DirectoryListingSupport.BuildEntryList(selectedDirectoryInfoEntries),
                    eof = index + 1 >= directoryEntries.Count,
                };
                READDIRPLUS3res candidateResult = CreateSuccessResult(
                    directoryResolution.PostOperationAttributes,
                    currentVerifier,
                    selectedEntries,
                    eof: index + 1 >= directoryEntries.Count);

                if (Nfs3DirectoryListingSupport.MeasureDirectoryInformationLength(directoryInformationListing) > directoryInformationBudget
                    || Nfs3DirectoryListingSupport.MeasureReadDirectoryPlusLength(candidateResult) > maximumCount)
                {
                    selectedEntries.RemoveAt(selectedEntries.Count - 1);
                    selectedDirectoryInfoEntries.RemoveAt(selectedDirectoryInfoEntries.Count - 1);
                    eof = false;

                    if (!appendedEntry)
                    {
                        return new READDIRPLUS3res
                        {
                            status = nfsstat3.NFS3ERR_TOOSMALL,
                            resfail = new READDIRPLUS3resfail
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

        protected override void WriteResult(READDIRPLUS3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static READDIRPLUS3res CreateSuccessResult(
            post_op_attr directoryAttributes,
            byte[] cookieVerifier,
            IReadOnlyList<entryplus3> entries,
            bool eof)
        {
            return new READDIRPLUS3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new READDIRPLUS3resok
                {
                    dir_attributes = directoryAttributes,
                    cookieverf = new cookieverf3
                    {
                        Value = cookieVerifier,
                    },
                    reply = new dirlistplus3
                    {
                        entries = Nfs3DirectoryListingSupport.BuildEntryPlusList(entries),
                        eof = eof,
                    },
                },
            };
        }
    }
}
