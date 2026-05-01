namespace OpenNFS.Protocol.V42.Server.Operations
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Implements the RFC 7862 sparse-file operations <c>SEEK</c>, <c>ALLOCATE</c>, <c>DEALLOCATE</c>,
    /// and <c>READ_PLUS</c> over the host <see cref="INfsSparse"/> capability.
    /// </summary>
    /// <remarks>
    /// The processor operates at the typed argument/result level using resolved host paths. A future
    /// V4.2 wire-level dispatcher resolves filehandles to host paths through the existing
    /// <c>IFileHandleProvider</c> seam and then calls into this processor. When the host has not
    /// implemented <see cref="INfsSparse"/>, the processor surfaces <c>NFS4ERR_NOTSUPP</c> for every
    /// operation, which is exactly what the v4.2 capability advertisement contract expects.
    /// </remarks>
    public sealed class Nfs42SparseFileProcessor
    {
        private readonly INfsSparse? sparseHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs42SparseFileProcessor"/> class.
        /// </summary>
        /// <param name="sparseHost">The host capability, or <c>null</c> when the host has not opted in.</param>
        public Nfs42SparseFileProcessor(INfsSparse? sparseHost)
        {
            this.sparseHost = sparseHost;
        }

        /// <summary>
        /// Processes a SEEK request.
        /// </summary>
        /// <param name="arguments">The SEEK arguments.</param>
        /// <param name="sourcePath">The host-local source path the client filehandle resolves to.</param>
        /// <returns>The result.</returns>
        public async ValueTask<SEEK4res> ProcessSeekAsync(SEEK4args arguments, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            if (sparseHost is null)
            {
                return new SEEK4res { sa_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.sa_offset is null || arguments.sa_what is null)
            {
                return new SEEK4res { sa_status = nfsstat4.NFS4ERR_INVAL };
            }

            bool searchForData = arguments.sa_what.Value == data_content4.NFS4_CONTENT_DATA;

            NfsSeekRequest request = new NfsSeekRequest(
                sourcePath,
                arguments.sa_offset.Value,
                searchForData);
            NfsSeekResponse response = await sparseHost.SeekAsync(request).ConfigureAwait(false);

            return new SEEK4res
            {
                sa_status = nfsstat4.NFS4_OK,
                resok4 = new seek_res4
                {
                    sr_eof = response.EndOfFile,
                    sr_offset = new offset4 { Value = response.Offset },
                },
            };
        }

        /// <summary>
        /// Processes an ALLOCATE request.
        /// </summary>
        /// <param name="arguments">The ALLOCATE arguments.</param>
        /// <param name="sourcePath">The host-local source path the client filehandle resolves to.</param>
        /// <returns>The result.</returns>
        public async ValueTask<ALLOCATE4res> ProcessAllocateAsync(ALLOCATE4args arguments, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            if (sparseHost is null)
            {
                return new ALLOCATE4res { ar_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.aa_offset is null || arguments.aa_length is null)
            {
                return new ALLOCATE4res { ar_status = nfsstat4.NFS4ERR_INVAL };
            }

            NfsAllocateRequest request = new NfsAllocateRequest(
                sourcePath,
                arguments.aa_offset.Value,
                arguments.aa_length.Value);
            await sparseHost.AllocateAsync(request).ConfigureAwait(false);

            return new ALLOCATE4res { ar_status = nfsstat4.NFS4_OK };
        }

        /// <summary>
        /// Processes a DEALLOCATE request.
        /// </summary>
        /// <param name="arguments">The DEALLOCATE arguments.</param>
        /// <param name="sourcePath">The host-local source path the client filehandle resolves to.</param>
        /// <returns>The result.</returns>
        public async ValueTask<DEALLOCATE4res> ProcessDeallocateAsync(DEALLOCATE4args arguments, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            if (sparseHost is null)
            {
                return new DEALLOCATE4res { dr_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.da_offset is null || arguments.da_length is null)
            {
                return new DEALLOCATE4res { dr_status = nfsstat4.NFS4ERR_INVAL };
            }

            NfsDeallocateRequest request = new NfsDeallocateRequest(
                sourcePath,
                arguments.da_offset.Value,
                arguments.da_length.Value);
            await sparseHost.DeallocateAsync(request).ConfigureAwait(false);

            return new DEALLOCATE4res { dr_status = nfsstat4.NFS4_OK };
        }

        /// <summary>
        /// Processes a READ_PLUS request.
        /// </summary>
        /// <param name="arguments">The READ_PLUS arguments.</param>
        /// <param name="sourcePath">The host-local source path the client filehandle resolves to.</param>
        /// <returns>The result.</returns>
        public async ValueTask<READ_PLUS4res> ProcessReadPlusAsync(READ_PLUS4args arguments, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            if (sparseHost is null)
            {
                return new READ_PLUS4res { rp_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.rpa_offset is null || arguments.rpa_count is null)
            {
                return new READ_PLUS4res { rp_status = nfsstat4.NFS4ERR_INVAL };
            }

            NfsReadSparseRequest request = new NfsReadSparseRequest(
                sourcePath,
                arguments.rpa_offset.Value,
                arguments.rpa_count.Value);
            NfsReadSparseResponse response = await sparseHost.ReadSparseAsync(request).ConfigureAwait(false);

            read_plus_content[] contents = new read_plus_content[response.Extents.Count];
            for (int index = 0; index < response.Extents.Count; index++)
            {
                NfsSparseExtent extent = response.Extents[index];
                if (extent.Kind == NfsSparseExtentKind.Data)
                {
                    contents[index] = new read_plus_content
                    {
                        rpc_content = data_content4.NFS4_CONTENT_DATA,
                        rpc_data = new data4
                        {
                            d_offset = new offset4 { Value = extent.Offset },
                            d_data = extent.GetData(),
                        },
                    };
                }
                else
                {
                    contents[index] = new read_plus_content
                    {
                        rpc_content = data_content4.NFS4_CONTENT_HOLE,
                        rpc_hole = new data_info4
                        {
                            di_offset = new offset4 { Value = extent.Offset },
                            di_length = new length4 { Value = extent.Length },
                        },
                    };
                }
            }

            return new READ_PLUS4res
            {
                rp_status = nfsstat4.NFS4_OK,
                rp_resok4 = new read_plus_res4
                {
                    rpr_eof = response.EndOfFile,
                    rpr_contents = contents,
                },
            };
        }
    }
}
