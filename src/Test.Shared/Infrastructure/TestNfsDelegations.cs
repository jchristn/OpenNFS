namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class TestNfsDelegations : INfsDelegations
    {
        private const uint ShareAccessWrite = 2U;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, NfsDelegationKind> _delegationsByPath;
        private readonly List<NfsRecallDelegationRequest> _recalls;
        private readonly List<NfsReturnDelegationRequest> _returns;

        internal TestNfsDelegations(IReadOnlyDictionary<string, NfsDelegationKind>? delegationsByPath = null)
        {
            _delegationsByPath = new Dictionary<string, NfsDelegationKind>(SeparatorAgnosticPathComparer.Instance);
            if (delegationsByPath is not null)
            {
                foreach (KeyValuePair<string, NfsDelegationKind> pair in delegationsByPath)
                {
                    _delegationsByPath[pair.Key] = pair.Value;
                }
            }

            _recalls = new List<NfsRecallDelegationRequest>();
            _returns = new List<NfsReturnDelegationRequest>();
        }

        public Task<NfsAcquireDelegationResponse> AcquireDelegationAsync(NfsAcquireDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            if (!_delegationsByPath.TryGetValue(request.SourcePath, out NfsDelegationKind delegationKind))
            {
                return Task.FromResult(NfsAcquireDelegationResponse.None);
            }

            if (request.PathKind != OpenNFS.Server.NfsPathKind.File)
            {
                return Task.FromResult(NfsAcquireDelegationResponse.None);
            }

            if (delegationKind == NfsDelegationKind.Read
                && (request.ShareAccess & ShareAccessWrite) != 0U)
            {
                return Task.FromResult(NfsAcquireDelegationResponse.None);
            }

            return Task.FromResult(new NfsAcquireDelegationResponse(delegationKind));
        }

        public Task RecallDelegationAsync(NfsRecallDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                _recalls.Add(request);
            }

            return Task.CompletedTask;
        }

        public Task ReturnDelegationAsync(NfsReturnDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                _returns.Add(request);
            }

            return Task.CompletedTask;
        }

        internal bool WasRecalled(string sourcePath)
        {
            string canonical = CanonicalizeSourcePath(sourcePath);
            lock (_syncRoot)
            {
                for (int index = 0; index < _recalls.Count; index++)
                {
                    if (string.Equals(CanonicalizeSourcePath(_recalls[index].SourcePath), canonical, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool WasReturned(string sourcePath)
        {
            string canonical = CanonicalizeSourcePath(sourcePath);
            lock (_syncRoot)
            {
                for (int index = 0; index < _returns.Count; index++)
                {
                    if (string.Equals(CanonicalizeSourcePath(_returns[index].SourcePath), canonical, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string CanonicalizeSourcePath(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return string.Empty;
            }

            // Normalize separators so a Windows-literal path and a server-side mixed-separator path
            // (Linux Path.Combine produces 'C:\\export/child' from a Windows-literal parent) compare
            // equal.
            return sourcePath.Replace('\\', '/');
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

                return string.Equals(CanonicalizeSourcePath(x), CanonicalizeSourcePath(y), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return CanonicalizeSourcePath(obj).GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }

        internal string DescribeRecalls()
        {
            lock (_syncRoot)
            {
                if (_recalls.Count == 0)
                {
                    return "<none>";
                }

                string[] descriptions = new string[_recalls.Count];
                for (int index = 0; index < _recalls.Count; index++)
                {
                    descriptions[index] = _recalls[index].SourcePath;
                }

                return string.Join(", ", descriptions);
            }
        }

        internal string DescribeReturns()
        {
            lock (_syncRoot)
            {
                if (_returns.Count == 0)
                {
                    return "<none>";
                }

                string[] descriptions = new string[_returns.Count];
                for (int index = 0; index < _returns.Count; index++)
                {
                    descriptions[index] = _returns[index].SourcePath;
                }

                return string.Join(", ", descriptions);
            }
        }
    }
}
