namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class StaticMountAuthorization : INfsMountAuthorization
    {
        private readonly Dictionary<string, NfsMountAccessDisposition> _dispositions;

        internal StaticMountAuthorization(IReadOnlyDictionary<string, NfsMountAccessDisposition> dispositions)
        {
            ArgumentNullException.ThrowIfNull(dispositions);
            _dispositions = new Dictionary<string, NfsMountAccessDisposition>(dispositions, StringComparer.Ordinal);
        }

        internal int InvocationCount { get; private set; }

        public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            InvocationCount++;

            if (_dispositions.TryGetValue(request.ExportDefinition.ExportPath, out NfsMountAccessDisposition disposition))
            {
                return Task.FromResult(new NfsAuthorizeMountResponse(disposition));
            }

            return Task.FromResult(new NfsAuthorizeMountResponse(NfsMountAccessDisposition.Allow));
        }
    }
}
