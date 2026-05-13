namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Path-first ergonomic facade over an established <see cref="OpenNfsV41ClientSession"/> that
    /// mirrors the v3 <see cref="OpenNFS.Client.OpenNfsMountSession"/> shape (grouped <c>Metadata</c>,
    /// <c>Files</c>, <c>Directories</c> members, path-first arguments).
    /// </summary>
    /// <remarks>
    /// The facade does not own the underlying session; disposing the facade is a no-op so callers
    /// retain control over the session lifetime. Each path-first call composes the corresponding
    /// COMPOUND through <see cref="OpenNfsV41PathOperations"/> and routes it through
    /// <see cref="OpenNfsV41ClientSession.TrySendCompoundAsync"/> so server-returned partial state and
    /// transport-level failures surface through a typed <see cref="OpenNfsV41CompoundResult"/>
    /// envelope instead of being flattened into a generic exception. The path-resolution contract
    /// matches the v3 mounted-session contract: every call re-resolves from <c>PUTROOTFH</c>, no
    /// client-side handle cache, and <c>..</c> segments are rejected.
    /// </remarks>
    public sealed class OpenNfsV41MountSession
    {
        private readonly OpenNfsV41ClientSession _session;

        internal OpenNfsV41MountSession(OpenNfsV41ClientSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            _session = session;
            Metadata = new OpenNfsV41MountSessionMetadata(session);
            Files = new OpenNfsV41MountSessionFiles(session);
            Directories = new OpenNfsV41MountSessionDirectories(session);
        }

        /// <summary>
        /// Gets the underlying NFSv4.1 client session that backs this facade.
        /// </summary>
        public OpenNfsV41ClientSession Session => _session;

        /// <summary>
        /// Gets the path-first metadata helpers (<c>GETATTR</c>).
        /// </summary>
        public OpenNfsV41MountSessionMetadata Metadata { get; }

        /// <summary>
        /// Gets the path-first file helpers (<c>READ</c>).
        /// </summary>
        public OpenNfsV41MountSessionFiles Files { get; }

        /// <summary>
        /// Gets the path-first directory helpers (<c>READDIR</c>).
        /// </summary>
        public OpenNfsV41MountSessionDirectories Directories { get; }
    }

    /// <summary>
    /// Path-first metadata helpers backed by <see cref="OpenNfsV41PathOperations.BuildGetAttributesOps"/>.
    /// </summary>
    public sealed class OpenNfsV41MountSessionMetadata
    {
        private readonly OpenNfsV41ClientSession _session;

        internal OpenNfsV41MountSessionMetadata(OpenNfsV41ClientSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>GETATTR</c> COMPOUND outcome for the
        /// supplied path under the stat-like default attribute mask.
        /// </summary>
        /// <param name="path">The path to query, relative to the export root.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> GetAttributesAsync(string path, CancellationToken cancellationToken)
        {
            return GetAttributesAsync(path, OpenNfsV41PathOperations.BuildStatLikeAttributeMask(), cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>GETATTR</c> COMPOUND outcome for the
        /// supplied path and attribute mask.
        /// </summary>
        /// <param name="path">The path to query, relative to the export root.</param>
        /// <param name="attributeMask">The attribute bitmap to request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> GetAttributesAsync(
            string path,
            bitmap4 attributeMask,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildGetAttributesOps(path, attributeMask);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-getattr", cancellationToken);
        }
    }

    /// <summary>
    /// Path-first file helpers backed by <see cref="OpenNfsV41PathOperations"/>.
    /// </summary>
    public sealed class OpenNfsV41MountSessionFiles
    {
        private readonly OpenNfsV41ClientSession _session;

        internal OpenNfsV41MountSessionFiles(OpenNfsV41ClientSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the existing-file <c>OPEN</c> COMPOUND outcome
        /// for the supplied path and open owner.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="clientId">The NFSv4.1 client id assigned by the server.</param>
        /// <param name="owner">The caller-stable open-owner identifier.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <param name="shareAccess">The OPEN4_SHARE_ACCESS_* value.</param>
        /// <param name="shareDeny">The OPEN4_SHARE_DENY_* value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> OpenExistingAsync(
            string path,
            ulong clientId,
            string owner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildOpenExistingOps(
                path, clientId, owner, sequenceId, shareAccess, shareDeny);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-open", cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the create <c>OPEN</c> COMPOUND outcome for the
        /// supplied path and open owner.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="clientId">The NFSv4.1 client id assigned by the server.</param>
        /// <param name="owner">The caller-stable open-owner identifier.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <param name="shareAccess">The OPEN4_SHARE_ACCESS_* value.</param>
        /// <param name="shareDeny">The OPEN4_SHARE_DENY_* value.</param>
        /// <param name="createMode">The create mode. Only <c>UNCHECKED4</c> and <c>GUARDED4</c>
        /// are supported by this convenience builder.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> CreateAndOpenAsync(
            string path,
            ulong clientId,
            string owner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            createmode4 createMode,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildCreateAndOpenOps(
                path, clientId, owner, sequenceId, shareAccess, shareDeny, createMode);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-create-open", cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>CLOSE</c> COMPOUND outcome for the
        /// supplied path and open stateid.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="stateid">The open state id to close.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> CloseAsync(
            string path,
            stateid4 stateid,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildCloseOps(path, stateid, sequenceId);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-close", cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>READ</c> COMPOUND outcome for the
        /// supplied path, stateid, offset, and count.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="stateid">The state id authorizing the read.</param>
        /// <param name="offset">The byte offset to begin reading at.</param>
        /// <param name="count">The maximum number of bytes to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> ReadAsync(
            string path,
            stateid4 stateid,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReadOps(path, stateid, offset, count);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-read", cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>WRITE</c> COMPOUND outcome for the
        /// supplied path, stateid, offset, stability request, and payload.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="stateid">The state id authorizing the write.</param>
        /// <param name="offset">The byte offset to begin writing at.</param>
        /// <param name="stable">The requested write stability.</param>
        /// <param name="data">The bytes to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> WriteAsync(
            string path,
            stateid4 stateid,
            ulong offset,
            stable_how4 stable,
            byte[] data,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildWriteOps(path, stateid, offset, stable, data);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-write", cancellationToken);
        }
    }

    /// <summary>
    /// Path-first directory helpers backed by <see cref="OpenNfsV41PathOperations.BuildReaddirOps"/>.
    /// </summary>
    public sealed class OpenNfsV41MountSessionDirectories
    {
        private readonly OpenNfsV41ClientSession _session;

        internal OpenNfsV41MountSessionDirectories(OpenNfsV41ClientSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>READDIR</c> COMPOUND outcome for the
        /// supplied directory path. Pass cookie 0 and an 8-byte zero verifier on the first call;
        /// pass the values returned by the previous reply for continuation.
        /// </summary>
        /// <param name="path">The directory path, relative to the export root.</param>
        /// <param name="cookie">The continuation cookie.</param>
        /// <param name="cookieVerifier">The 8-byte continuation verifier.</param>
        /// <param name="dircount">Maximum bytes the server may return for entry names + cookies.</param>
        /// <param name="maxcount">Maximum bytes the server may return overall.</param>
        /// <param name="attributeMask">The per-entry attribute bitmap to request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> ListAsync(
            string path,
            ulong cookie,
            byte[] cookieVerifier,
            uint dircount,
            uint maxcount,
            bitmap4 attributeMask,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReaddirOps(
                path, cookie, cookieVerifier, dircount, maxcount, attributeMask);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-readdir", cancellationToken);
        }

        /// <summary>
        /// Returns a non-throwing envelope containing the <c>REMOVE</c> COMPOUND outcome for the
        /// supplied file or directory-entry path.
        /// </summary>
        /// <param name="path">The entry path, relative to the export root.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The envelope.</returns>
        public Task<OpenNfsV41CompoundResult> RemoveAsync(string path, CancellationToken cancellationToken)
        {
            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildRemoveOps(path);
            return _session.TrySendCompoundAsync(ops, cacheReply: false, tag: "v41-mount-remove", cancellationToken);
        }
    }
}
