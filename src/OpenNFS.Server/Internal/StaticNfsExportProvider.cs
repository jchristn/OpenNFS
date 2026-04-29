namespace OpenNFS.Server.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class StaticNfsExportProvider : INfsExportProvider
    {
        private readonly OpenNfsExportDefinition[] _ExportDefinitions;

        internal StaticNfsExportProvider(IReadOnlyCollection<OpenNfsExportDefinition> exportDefinitions)
        {
            ArgumentNullException.ThrowIfNull(exportDefinitions);

            _ExportDefinitions = new OpenNfsExportDefinition[exportDefinitions.Count];
            int index = 0;

            foreach (OpenNfsExportDefinition? exportDefinition in exportDefinitions)
            {
                if (exportDefinition is null)
                {
                    throw new ArgumentException("The static export set cannot contain null export definitions.", nameof(exportDefinitions));
                }

                _ExportDefinitions[index++] = exportDefinition;
            }
        }

        public Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsGetExportsResponse(_ExportDefinitions));
        }
    }
}
