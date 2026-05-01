namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Identity;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class TestNfsIdMapper : INfsIdMapper
    {
        private readonly Dictionary<string, NfsIdentityMapping> _entriesByPath;
        private readonly string _owner;
        private readonly string _ownerGroup;

        internal TestNfsIdMapper(string owner = "owner@example.test", string ownerGroup = "group@example.test")
        {
            _entriesByPath = new Dictionary<string, NfsIdentityMapping>(StringComparer.OrdinalIgnoreCase);
            _owner = owner;
            _ownerGroup = ownerGroup;
        }

        public Task<NfsGetIdentityResponse> GetIdentityAsync(NfsGetIdentityRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            string normalizedPath = NormalizePath(request.SourcePath);
            if (_entriesByPath.TryGetValue(normalizedPath, out NfsIdentityMapping? mapping))
            {
                return Task.FromResult(new NfsGetIdentityResponse(mapping));
            }

            return Task.FromResult(new NfsGetIdentityResponse(_owner, _ownerGroup));
        }

        public Task<NfsSetIdentityResponse> SetIdentityAsync(NfsSetIdentityRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            string normalizedPath = NormalizePath(request.SourcePath);
            NfsIdentityMapping currentIdentity = _entriesByPath.TryGetValue(normalizedPath, out NfsIdentityMapping? existingMapping)
                ? existingMapping
                : new NfsIdentityMapping(_owner, _ownerGroup);
            NfsIdentityMapping updatedIdentity = new NfsIdentityMapping(
                request.Owner ?? currentIdentity.Owner,
                request.OwnerGroup ?? currentIdentity.OwnerGroup);
            _entriesByPath[normalizedPath] = updatedIdentity;
            return Task.FromResult(new NfsSetIdentityResponse(updatedIdentity));
        }

        private static string NormalizePath(string path)
        {
            return System.IO.Path.GetFullPath(path)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }
    }
}
