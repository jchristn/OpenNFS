namespace Test.Shared.Infrastructure
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class TestNfsIdMapper : INfsIdMapper
    {
        private readonly string _owner;
        private readonly string _ownerGroup;

        internal TestNfsIdMapper(string owner = "owner@example.test", string ownerGroup = "group@example.test")
        {
            _owner = owner;
            _ownerGroup = ownerGroup;
        }

        public Task<NfsGetIdentityResponse> GetIdentityAsync(NfsGetIdentityRequest request)
        {
            return Task.FromResult(new NfsGetIdentityResponse(_owner, _ownerGroup));
        }
    }
}
