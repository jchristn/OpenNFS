namespace OpenNFS.Server.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class CompositeNfsExportProvider : INfsExportProvider
    {
        private readonly INfsExportProvider[] _Providers;

        internal CompositeNfsExportProvider(IReadOnlyList<INfsExportProvider> providers)
        {
            ArgumentNullException.ThrowIfNull(providers);

            if (providers.Count < 1)
            {
                throw new ArgumentException("At least one export provider is required when composing export providers.", nameof(providers));
            }

            _Providers = new INfsExportProvider[providers.Count];

            for (int index = 0; index < providers.Count; index++)
            {
                INfsExportProvider? provider = providers[index];
                if (provider is null)
                {
                    throw new ArgumentException("Composed export providers cannot include null entries.", nameof(providers));
                }

                _Providers[index] = provider;
            }
        }

        public async Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            List<OpenNfsExportDefinition> combinedExports = new List<OpenNfsExportDefinition>();

            foreach (INfsExportProvider provider in _Providers)
            {
                request.CancellationToken.ThrowIfCancellationRequested();

                NfsGetExportsResponse? response =
                    await provider.GetExportsAsync(request).ConfigureAwait(false);

                if (response is null)
                {
                    throw new InvalidOperationException("A composed export provider returned null instead of an export response.");
                }

                foreach (OpenNfsExportDefinition? exportDefinition in response.Exports)
                {
                    if (exportDefinition is null)
                    {
                        throw new InvalidOperationException("A composed export provider returned a null export definition.");
                    }

                    combinedExports.Add(exportDefinition);
                }
            }

            return new NfsGetExportsResponse(combinedExports);
        }
    }
}
