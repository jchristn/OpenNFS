namespace Test.Shared.Infrastructure
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class TestNfsCopyClone : INfsCopyClone
    {
        public ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request)
        {
            return ValueTask.FromResult(new NfsCopyResponse(request.Count, committedToStableStorage: true));
        }

        public ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request)
        {
            return ValueTask.FromResult(NfsCloneResponse.Success);
        }
    }
}
