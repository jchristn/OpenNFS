namespace Sample.OpenNfsServer.Providers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Sample.OpenNfsServer.State;

    internal sealed class SampleDurableFileSystem : INfsFileSystem, INfsLocking, INfsAcls, INfsDelegations, INfsIdMapper, INfsAttributeMutation
    {
        private readonly SamplePersistentAclStore _aclStore;
        private readonly SampleDelegationManager _delegationManager;
        private readonly LocalNfsFileSystem _fileSystem;
        private readonly SamplePersistentIdentityStore _identityStore;
        private readonly SampleInMemoryLockManager _lockManager;

        internal SampleDurableFileSystem(
            string sourceRoot,
            string mappingPath,
            string owner,
            string ownerGroup)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(mappingPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(owner);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGroup);

            string stateDirectory = Path.GetDirectoryName(Path.GetFullPath(mappingPath)) ?? Path.GetFullPath(sourceRoot);
            Directory.CreateDirectory(stateDirectory);

            _aclStore = new SamplePersistentAclStore(
                Path.Combine(stateDirectory, "sample-acls.json"),
                owner,
                ownerGroup);
            _identityStore = new SamplePersistentIdentityStore(
                Path.Combine(stateDirectory, "sample-identities.json"),
                owner,
                ownerGroup);
            _delegationManager = new SampleDelegationManager();
            _fileSystem = LocalNfsFileSystem.Default;
            _lockManager = new SampleInMemoryLockManager();
        }

        public Task<NfsAcquireDelegationResponse> AcquireDelegationAsync(NfsAcquireDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_delegationManager.Acquire(request));
        }

        public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            return _fileSystem.CommitFileAsync(request);
        }

        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            return _fileSystem.CreateHardLinkAsync(request);
        }

        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            return _fileSystem.CreatePathAsync(request);
        }

        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            return _fileSystem.CreateSymbolicLinkAsync(request);
        }

        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            return _fileSystem.DeletePathAsync(request);
        }

        public Task<NfsGetAclResponse> GetAclAsync(NfsGetAclRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new NfsGetAclResponse(
                NfsAclSupport.AllowAcl | NfsAclSupport.DenyAcl,
                _aclStore.GetEntries(request.SourcePath)));
        }

        public Task<NfsGetIdentityResponse> GetIdentityAsync(NfsGetIdentityRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsGetIdentityResponse(_identityStore.GetIdentity(request.SourcePath)));
        }

        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            return _fileSystem.GetPathInfoAsync(request);
        }

        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            return _fileSystem.LookupPathAsync(request);
        }

        public Task<NfsLockResponse> ProcessLockAsync(NfsLockRequest request)
        {
            return _lockManager.ProcessAsync(request);
        }

        public Task RecallDelegationAsync(NfsRecallDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            _delegationManager.RecordRecall(request);
            return Task.CompletedTask;
        }

        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            return _fileSystem.ReadDirectoryAsync(request);
        }

        public Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            return _fileSystem.ReadFileAsync(request);
        }

        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            return _fileSystem.ReadSymbolicLinkAsync(request);
        }

        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            return _fileSystem.RenamePathAsync(request);
        }

        public Task ReturnDelegationAsync(NfsReturnDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            _delegationManager.RecordReturn(request);
            return Task.CompletedTask;
        }

        public Task<NfsSetAclResponse> SetAclAsync(NfsSetAclRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            _aclStore.SetEntries(request.SourcePath, request.Entries);
            return Task.FromResult(new NfsSetAclResponse(
                NfsAclSupport.AllowAcl | NfsAclSupport.DenyAcl,
                _aclStore.GetEntries(request.SourcePath)));
        }

        public Task<NfsSetAttributesResponse> SetAttributesAsync(NfsSetAttributesRequest request)
        {
            return _fileSystem.SetAttributesAsync(request);
        }

        public Task<NfsSetIdentityResponse> SetIdentityAsync(NfsSetIdentityRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new NfsSetIdentityResponse(
                _identityStore.SetIdentity(
                    request.SourcePath,
                    request.Owner,
                    request.OwnerGroup)));
        }

        public Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            return _fileSystem.WriteFileAsync(request);
        }
    }
}
