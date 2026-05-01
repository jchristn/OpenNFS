namespace OpenNFS.Protocol.V42.Server.Operations
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Implements the RFC 7862 server-side copy and clone operations <c>COPY</c> and <c>CLONE</c> over
    /// the host <see cref="INfsCopyClone"/> capability.
    /// </summary>
    /// <remarks>
    /// The processor operates at the typed argument/result level using resolved host paths. A future
    /// V4.2 wire-level dispatcher resolves source and destination filehandles to host paths through the
    /// existing <c>IFileHandleProvider</c> seam and then calls into this processor. When the host has
    /// not implemented <see cref="INfsCopyClone"/>, the processor surfaces <c>NFS4ERR_NOTSUPP</c> for
    /// every operation, which is exactly what the v4.2 capability advertisement contract expects.
    /// </remarks>
    public sealed class Nfs42CopyCloneProcessor
    {
        private readonly INfsCopyClone? copyCloneHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs42CopyCloneProcessor"/> class.
        /// </summary>
        /// <param name="copyCloneHost">The host capability, or <c>null</c> when the host has not opted in.</param>
        public Nfs42CopyCloneProcessor(INfsCopyClone? copyCloneHost)
        {
            this.copyCloneHost = copyCloneHost;
        }

        /// <summary>
        /// Processes a COPY request.
        /// </summary>
        /// <param name="arguments">The COPY arguments.</param>
        /// <param name="sourcePath">Resolved host-local source path.</param>
        /// <param name="destinationPath">Resolved host-local destination path.</param>
        /// <returns>The result.</returns>
        public async ValueTask<COPY4res> ProcessCopyAsync(COPY4args arguments, string sourcePath, string destinationPath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            if (copyCloneHost is null)
            {
                return new COPY4res { cr_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            // RFC 7862 §15.2 explicitly forbids inter-server COPY through the source-server list when
            // the server does not advertise that capability. The current OpenNFS server implementation
            // only supports intra-server copies; reject any cross-server COPY up front.
            if (arguments.ca_source_server is { Length: > 0 })
            {
                return new COPY4res { cr_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.ca_src_offset is null || arguments.ca_dst_offset is null || arguments.ca_count is null)
            {
                return new COPY4res { cr_status = nfsstat4.NFS4ERR_INVAL };
            }

            NfsCopyRequest request = new NfsCopyRequest(
                sourcePath,
                destinationPath,
                arguments.ca_src_offset.Value,
                arguments.ca_dst_offset.Value,
                arguments.ca_count.Value,
                arguments.ca_synchronous);

            NfsCopyResponse response = await copyCloneHost.CopyAsync(request).ConfigureAwait(false);

            return new COPY4res
            {
                cr_status = nfsstat4.NFS4_OK,
                cr_resok4 = new COPY4resok
                {
                    cr_response = new write_response4
                    {
                        wr_callback_id = Array.Empty<stateid4>(),
                        wr_count = new length4 { Value = response.BytesCopied },
                        wr_committed = response.CommittedToStableStorage ? stable_how4.FILE_SYNC4 : stable_how4.UNSTABLE4,
                        wr_writeverf = new verifier4 { Value = new byte[8] },
                    },
                    cr_requirements = new copy_requirements4(),
                },
            };
        }

        /// <summary>
        /// Processes a CLONE request.
        /// </summary>
        /// <param name="arguments">The CLONE arguments.</param>
        /// <param name="sourcePath">Resolved host-local source path.</param>
        /// <param name="destinationPath">Resolved host-local destination path.</param>
        /// <returns>The result.</returns>
        public async ValueTask<CLONE4res> ProcessCloneAsync(CLONE4args arguments, string sourcePath, string destinationPath)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            if (copyCloneHost is null)
            {
                return new CLONE4res { cl_status = nfsstat4.NFS4ERR_NOTSUPP };
            }

            if (arguments.cl_src_offset is null || arguments.cl_dst_offset is null || arguments.cl_count is null)
            {
                return new CLONE4res { cl_status = nfsstat4.NFS4ERR_INVAL };
            }

            NfsCloneRequest request = new NfsCloneRequest(
                sourcePath,
                destinationPath,
                arguments.cl_src_offset.Value,
                arguments.cl_dst_offset.Value,
                arguments.cl_count.Value);

            await copyCloneHost.CloneAsync(request).ConfigureAwait(false);

            return new CLONE4res { cl_status = nfsstat4.NFS4_OK };
        }
    }
}
