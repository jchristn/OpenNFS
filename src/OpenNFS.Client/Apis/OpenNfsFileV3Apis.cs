namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Encapsulates grouped NFSv3 file flows.
    /// </summary>
    internal sealed class OpenNfsFileV3Apis
    {
        private readonly OpenNfsFileV3MetadataApis _metadata;
        private readonly OpenNfsFileV3IoApis _io;
        private readonly OpenNfsFileV3FileSystemApis _fileSystem;

        internal OpenNfsFileV3Apis(OpenNfsClient client)
        {
            _metadata = new OpenNfsFileV3MetadataApis(client);
            _io = new OpenNfsFileV3IoApis(client);
            _fileSystem = new OpenNfsFileV3FileSystemApis(client);
        }

        public Task<OpenNfsV3GetAttributesResult> GetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _metadata.GetAttributesV3Async(fileHandle, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareGetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _metadata.PrepareGetAttributesV3Async(fileHandle, cancellationToken);
        }

        public OpenNfsV3GetAttributesResult ReadGetAttributesV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _metadata.ReadGetAttributesV3Result(encodedReply);
        }

        public Task<OpenNfsV3AccessResult> AccessV3Async(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _metadata.AccessV3Async(fileHandle, requestedAccess, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareAccessV3Async(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _metadata.PrepareAccessV3Async(fileHandle, requestedAccess, cancellationToken);
        }

        public OpenNfsV3AccessResult ReadAccessV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _metadata.ReadAccessV3Result(encodedReply);
        }

        public Task<OpenNfsV3ReadResult> ReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _io.ReadV3Async(fileHandle, offset, count, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _io.PrepareReadV3Async(fileHandle, offset, count, cancellationToken);
        }

        public OpenNfsV3ReadResult ReadReadV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _io.ReadReadV3Result(encodedReply);
        }

        public Task<OpenNfsV3ReadLinkResult> ReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _io.ReadLinkV3Async(symbolicLinkHandle, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _io.PrepareReadLinkV3Async(symbolicLinkHandle, cancellationToken);
        }

        public OpenNfsV3ReadLinkResult ReadReadLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _io.ReadReadLinkV3Result(encodedReply);
        }

        public Task<OpenNfsV3WriteResult> WriteV3Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _io.WriteV3Async(fileHandle, offset, stability, data, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareWriteV3Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _io.PrepareWriteV3Async(fileHandle, offset, stability, data, cancellationToken);
        }

        public OpenNfsV3WriteResult ReadWriteV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _io.ReadWriteV3Result(encodedReply);
        }

        public Task<OpenNfsV3CommitResult> CommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _io.CommitV3Async(fileHandle, offset, count, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _io.PrepareCommitV3Async(fileHandle, offset, count, cancellationToken);
        }

        public OpenNfsV3CommitResult ReadCommitV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _io.ReadCommitV3Result(encodedReply);
        }

        public Task<OpenNfsV3FileSystemStatusResult> GetFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.GetFileSystemStatusV3Async(fileHandle, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.PrepareFileSystemStatusV3Async(fileHandle, cancellationToken);
        }

        public OpenNfsV3FileSystemStatusResult ReadFileSystemStatusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _fileSystem.ReadFileSystemStatusV3Result(encodedReply);
        }

        public Task<OpenNfsV3FileSystemInfoResult> GetFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.GetFileSystemInfoV3Async(fileHandle, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.PrepareFileSystemInfoV3Async(fileHandle, cancellationToken);
        }

        public OpenNfsV3FileSystemInfoResult ReadFileSystemInfoV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _fileSystem.ReadFileSystemInfoV3Result(encodedReply);
        }

        public Task<OpenNfsV3PathConfigurationResult> GetPathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.GetPathConfigurationV3Async(fileHandle, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PreparePathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _fileSystem.PreparePathConfigurationV3Async(fileHandle, cancellationToken);
        }

        public OpenNfsV3PathConfigurationResult ReadPathConfigurationV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _fileSystem.ReadPathConfigurationV3Result(encodedReply);
        }
    }
}
