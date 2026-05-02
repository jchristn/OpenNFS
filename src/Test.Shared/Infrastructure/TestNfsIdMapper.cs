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

        private static bool IsAnySeparator(char value)
        {
            return value == '/' || value == '\\';
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
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            // Trim trailing separators and canonicalize '\' to '/' so the same path matches whether
            // the caller used Windows-literal syntax or a path that picked up a Linux '/' from
            // server-side Path.Combine on a runner where DirectorySeparatorChar is '/'.
            string trimmed = path;
            while (trimmed.Length > 0 && IsAnySeparator(trimmed[trimmed.Length - 1]))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            return trimmed.Replace('\\', '/');
        }
    }
}
