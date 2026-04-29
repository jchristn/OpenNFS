namespace OpenNFS.Server.Abstractions
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Resolves host-local source paths for export validation and later NFS request handling.
    /// </summary>
    public interface INfsFileSystem
    {
        /// <summary>
        /// Resolves information about a host-local source path.
        /// </summary>
        /// <param name="request">Request context for the path-resolution operation.</param>
        /// <returns>Resolved path information for the supplied source path.</returns>
        Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request);

        /// <summary>
        /// Resolves a child entry beneath a host-local directory path.
        /// </summary>
        /// <param name="request">Request context for the child-lookup operation.</param>
        /// <returns>Resolved path information for the requested child entry.</returns>
        Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request);

        /// <summary>
        /// Enumerates child entries beneath a host-local directory path.
        /// </summary>
        /// <param name="request">Request context for the directory-read operation.</param>
        /// <returns>The ordered child entries discovered beneath the requested directory path.</returns>
        Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request);

        /// <summary>
        /// Reads a byte range from a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-read operation.</param>
        /// <returns>The file-read result for the supplied path and byte range.</returns>
        Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request);

        /// <summary>
        /// Reads the target path from a host-local symbolic link path.
        /// </summary>
        /// <param name="request">Request context for the symbolic-link-read operation.</param>
        /// <returns>The symbolic-link-read result for the supplied path.</returns>
        Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request);

        /// <summary>
        /// Writes a byte range to a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-write operation.</param>
        /// <returns>The file-write result for the supplied path, byte range, and stability mode.</returns>
        Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request);

        /// <summary>
        /// Commits previously acknowledged writes for a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-commit operation.</param>
        /// <returns>The file-commit result for the supplied path and byte range.</returns>
        Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request);

        /// <summary>
        /// Creates a new host-local filesystem entry beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the create operation.</param>
        /// <returns>The create result for the supplied parent path, entry name, and requested kind.</returns>
        Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request);

        /// <summary>
        /// Creates a new host-local symbolic link beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the symbolic-link-create operation.</param>
        /// <returns>The create result for the supplied parent path, entry name, and symbolic-link target.</returns>
        Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request);

        /// <summary>
        /// Creates a new host-local hard link beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the hard-link-create operation.</param>
        /// <returns>The create result for the supplied source path, destination parent path, and destination entry name.</returns>
        Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request);

        /// <summary>
        /// Deletes a host-local filesystem entry beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the delete operation.</param>
        /// <returns>The delete result for the supplied parent path, entry name, and expected kind.</returns>
        Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request);

        /// <summary>
        /// Renames or moves a host-local filesystem entry between directory paths.
        /// </summary>
        /// <param name="request">Request context for the rename operation.</param>
        /// <returns>The rename result for the supplied source and destination directory paths and entry names.</returns>
        Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request);
    }
}
