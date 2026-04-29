namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class StaticTestNfsExportProvider : INfsExportProvider
    {
        private readonly OpenNfsExportDefinition[] _ExportDefinitions;
        private int _InvocationCount;

        internal StaticTestNfsExportProvider(IReadOnlyCollection<OpenNfsExportDefinition> exportDefinitions)
        {
            ArgumentNullException.ThrowIfNull(exportDefinitions);

            _ExportDefinitions = new OpenNfsExportDefinition[exportDefinitions.Count];
            int index = 0;

            foreach (OpenNfsExportDefinition? exportDefinition in exportDefinitions)
            {
                if (exportDefinition is null)
                {
                    throw new ArgumentException("The test export provider cannot contain null export definitions.", nameof(exportDefinitions));
                }

                _ExportDefinitions[index++] = exportDefinition;
            }
        }

        internal int InvocationCount
        {
            get
            {
                return System.Threading.Volatile.Read(ref _InvocationCount);
            }
        }

        public Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            System.Threading.Interlocked.Increment(ref _InvocationCount);
            return Task.FromResult(new NfsGetExportsResponse(_ExportDefinitions));
        }
    }
}
