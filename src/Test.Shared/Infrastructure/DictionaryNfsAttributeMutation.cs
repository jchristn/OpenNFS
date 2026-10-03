namespace Test.Shared.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Opt-in <see cref="INfsAttributeMutation"/> over a <see cref="CapabilityAwareDictionaryNfsFileSystem"/>, for hosts that
    /// must accept the create attributes (mode, and size on <c>O_TRUNC</c>) the Linux kernel client sends on NFSv4.0 OPEN.
    /// </summary>
    internal sealed class DictionaryNfsAttributeMutation : INfsAttributeMutation
    {
        private readonly CapabilityAwareDictionaryNfsFileSystem _FileSystem;

        internal DictionaryNfsAttributeMutation(CapabilityAwareDictionaryNfsFileSystem fileSystem)
        {
            _FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public Task<NfsSetAttributesResponse> SetAttributesAsync(NfsSetAttributesRequest request)
        {
            return _FileSystem.SetAttributesAsync(request);
        }
    }
}
