namespace OpenNFS.Server.Internal
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class EmptyNfsExportProvider : INfsExportProvider
    {
        internal static EmptyNfsExportProvider Instance { get; } = new EmptyNfsExportProvider();

        public Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsGetExportsResponse(System.Array.Empty<OpenNfsExportDefinition>()));
        }
    }
}
