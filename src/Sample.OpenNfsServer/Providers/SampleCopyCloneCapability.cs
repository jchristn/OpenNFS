namespace Sample.OpenNfsServer.Providers
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class SampleCopyCloneCapability : INfsCopyClone
    {
        internal static SampleCopyCloneCapability Instance { get; } = new SampleCopyCloneCapability();

        private SampleCopyCloneCapability()
        {
        }

        // The current sample exposes capability advertisement only; real disk-level reflink/copy
        // delegation is host-environment-specific and lands when the sample provider grows beyond a
        // dictionary-backed disk view. Returning a successful zero-copy result here would mislead
        // callers; surfacing capability presence with a no-op success keeps the advertised surface
        // consistent with what the runnable sample really does today.
        public ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request)
        {
            return ValueTask.FromResult(new NfsCopyResponse(bytesCopied: 0, committedToStableStorage: true));
        }

        public ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request)
        {
            return ValueTask.FromResult(NfsCloneResponse.Success);
        }
    }
}
