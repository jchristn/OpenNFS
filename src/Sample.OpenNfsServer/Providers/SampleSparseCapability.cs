namespace Sample.OpenNfsServer.Providers
{
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class SampleSparseCapability : INfsSparse
    {
        internal static SampleSparseCapability Instance { get; } = new SampleSparseCapability();

        private SampleSparseCapability()
        {
        }

        public ValueTask<NfsSeekResponse> SeekAsync(NfsSeekRequest request)
        {
            // The sample provider treats every file as fully-allocated, so the next data
            // boundary from any in-range offset is the offset itself, and the next hole is
            // end-of-file. Hosts that back real sparse files override this with extent-aware
            // logic in their own INfsSparse implementation.
            return ValueTask.FromResult(new NfsSeekResponse(request.Offset, !request.SearchForData));
        }

        public ValueTask<NfsAllocateResponse> AllocateAsync(NfsAllocateRequest request)
        {
            return ValueTask.FromResult(NfsAllocateResponse.Success);
        }

        public ValueTask<NfsDeallocateResponse> DeallocateAsync(NfsDeallocateRequest request)
        {
            return ValueTask.FromResult(NfsDeallocateResponse.Success);
        }

        public ValueTask<NfsReadSparseResponse> ReadSparseAsync(NfsReadSparseRequest request)
        {
            byte[] payload = new byte[request.Count];
            return ValueTask.FromResult(
                new NfsReadSparseResponse(
                    new[] { NfsSparseExtent.ForData(request.Offset, payload) },
                    endOfFile: false));
        }
    }
}
