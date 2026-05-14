namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Encapsulates grouped NFSv3 directory and namespace flows.
    /// </summary>
    internal sealed class OpenNfsDirectoryV3Apis
    {
        private readonly OpenNfsDirectoryV3LookupApis _lookup;
        private readonly OpenNfsDirectoryV3ReadApis _reads;
        private readonly OpenNfsDirectoryV3MutationApis _mutations;

        internal OpenNfsDirectoryV3Apis(OpenNfsClient client)
        {
            _lookup = new OpenNfsDirectoryV3LookupApis(client);
            _reads = new OpenNfsDirectoryV3ReadApis(client);
            _mutations = new OpenNfsDirectoryV3MutationApis(client);
        }

        public Task<OpenNfsV3LookupResult> LookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _lookup.LookupV3Async(directoryHandle, entryName, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareLookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _lookup.PrepareLookupV3Async(directoryHandle, entryName, cancellationToken);
        }

        public OpenNfsV3LookupResult ReadLookupV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _lookup.ReadLookupV3Result(encodedReply);
        }

        public Task<OpenNfsV3ReadDirectoryResult> ReadDirectoryV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count,
            CancellationToken cancellationToken)
        {
            return _reads.ReadDirectoryV3Async(directoryHandle, cookie, cookieVerifier, count, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadDirectoryV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count,
            CancellationToken cancellationToken)
        {
            return _reads.PrepareReadDirectoryV3Async(directoryHandle, cookie, cookieVerifier, count, cancellationToken);
        }

        public OpenNfsV3ReadDirectoryResult ReadReadDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _reads.ReadReadDirectoryV3Result(encodedReply);
        }

        public Task<OpenNfsV3ReadDirectoryPlusResult> ReadDirectoryPlusV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _reads.ReadDirectoryPlusV3Async(directoryHandle, cookie, cookieVerifier, directoryCount, maxCount, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadDirectoryPlusV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _reads.PrepareReadDirectoryPlusV3Async(directoryHandle, cookie, cookieVerifier, directoryCount, maxCount, cancellationToken);
        }

        public OpenNfsV3ReadDirectoryPlusResult ReadReadDirectoryPlusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _reads.ReadReadDirectoryPlusV3Result(encodedReply);
        }

        public Task<OpenNfsV3CreatePathResult> CreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _mutations.CreateFileV3Async(directoryHandle, entryName, failIfExists, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareCreateFileV3Async(directoryHandle, entryName, failIfExists, cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadCreateFileV3Result(encodedReply);
        }

        public Task<OpenNfsV3CreatePathResult> CreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.CreateDirectoryV3Async(directoryHandle, entryName, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareCreateDirectoryV3Async(directoryHandle, entryName, cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadCreateDirectoryV3Result(encodedReply);
        }

        public Task<OpenNfsV3DirectoryMutationResult> RemoveFileV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.RemoveFileV3Async(directoryHandle, entryName, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveFileV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareRemoveFileV3Async(directoryHandle, entryName, cancellationToken);
        }

        public OpenNfsV3DirectoryMutationResult ReadRemoveFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadRemoveFileV3Result(encodedReply);
        }

        public Task<OpenNfsV3DirectoryMutationResult> RemoveDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.RemoveDirectoryV3Async(directoryHandle, entryName, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareRemoveDirectoryV3Async(directoryHandle, entryName, cancellationToken);
        }

        public OpenNfsV3DirectoryMutationResult ReadRemoveDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadRemoveDirectoryV3Result(encodedReply);
        }

        public Task<OpenNfsV3RenameResult> RenameV3Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _mutations.RenameV3Async(
                sourceDirectoryHandle,
                sourceEntryName,
                destinationDirectoryHandle,
                destinationEntryName,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRenameV3Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareRenameV3Async(
                sourceDirectoryHandle,
                sourceEntryName,
                destinationDirectoryHandle,
                destinationEntryName,
                cancellationToken);
        }

        public OpenNfsV3RenameResult ReadRenameV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadRenameV3Result(encodedReply);
        }

        public Task<OpenNfsV3CreatePathResult> CreateSymbolicLinkV3Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _mutations.CreateSymbolicLinkV3Async(directoryHandle, entryName, targetPath, cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateSymbolicLinkV3Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareCreateSymbolicLinkV3Async(directoryHandle, entryName, targetPath, cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateSymbolicLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadCreateSymbolicLinkV3Result(encodedReply);
        }

        public Task<OpenNfsV3LinkResult> CreateHardLinkV3Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _mutations.CreateHardLinkV3Async(
                sourceFileHandle,
                destinationDirectoryHandle,
                destinationEntryName,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateHardLinkV3Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _mutations.PrepareCreateHardLinkV3Async(
                sourceFileHandle,
                destinationDirectoryHandle,
                destinationEntryName,
                cancellationToken);
        }

        public OpenNfsV3LinkResult ReadCreateHardLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return _mutations.ReadCreateHardLinkV3Result(encodedReply);
        }
    }
}
