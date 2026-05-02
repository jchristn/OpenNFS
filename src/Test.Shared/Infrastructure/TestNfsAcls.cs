namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class TestNfsAcls : INfsAcls
    {
        private readonly Dictionary<string, NfsAclEntry[]> _entriesByPath;

        internal TestNfsAcls(
            NfsAclSupport supportedAcls = NfsAclSupport.AllowAcl | NfsAclSupport.DenyAcl,
            IReadOnlyDictionary<string, IReadOnlyList<NfsAclEntry>>? initialEntries = null)
        {
            SupportedAcls = supportedAcls;
            _entriesByPath = new Dictionary<string, NfsAclEntry[]>(SeparatorAgnosticPathComparer.Instance);

            if (initialEntries is null)
            {
                return;
            }

            foreach (KeyValuePair<string, IReadOnlyList<NfsAclEntry>> pair in initialEntries)
            {
                _entriesByPath[pair.Key] = CopyEntries(pair.Value);
            }
        }

        internal NfsAclSupport SupportedAcls { get; }

        public Task<NfsGetAclResponse> GetAclAsync(NfsGetAclRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsAclEntry[] entries = _entriesByPath.TryGetValue(request.SourcePath, out NfsAclEntry[]? storedEntries)
                ? CopyEntries(storedEntries)
                : Array.Empty<NfsAclEntry>();
            return Task.FromResult(new NfsGetAclResponse(SupportedAcls, entries));
        }

        public Task<NfsSetAclResponse> SetAclAsync(NfsSetAclRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsAclEntry[] entries = CopyEntries(request.Entries);
            _entriesByPath[request.SourcePath] = entries;
            return Task.FromResult(new NfsSetAclResponse(SupportedAcls, CopyEntries(entries)));
        }

        private static NfsAclEntry[] CopyEntries(IReadOnlyList<NfsAclEntry> entries)
        {
            NfsAclEntry[] copy = new NfsAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            return copy;
        }

        private sealed class SeparatorAgnosticPathComparer : IEqualityComparer<string>
        {
            internal static SeparatorAgnosticPathComparer Instance { get; } = new SeparatorAgnosticPathComparer();

            public bool Equals(string? x, string? y)
            {
                if (x is null)
                {
                    return y is null;
                }

                if (y is null)
                {
                    return false;
                }

                return string.Equals(Canonicalize(x), Canonicalize(y), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return Canonicalize(obj).GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            private static string Canonicalize(string value)
            {
                return value.Replace('\\', '/');
            }
        }
    }
}
