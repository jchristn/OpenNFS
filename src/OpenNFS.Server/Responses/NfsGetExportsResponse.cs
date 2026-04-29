namespace OpenNFS.Server.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response context containing the exports exposed by the configured host.
    /// </summary>
    public sealed class NfsGetExportsResponse
    {
        private readonly OpenNfsExportDefinition[] _Exports;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetExportsResponse"/> class.
        /// </summary>
        /// <param name="exports">Exports exposed by the configured host.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exports"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="exports"/> contains a null export definition.</exception>
        public NfsGetExportsResponse(IReadOnlyCollection<OpenNfsExportDefinition> exports)
        {
            ArgumentNullException.ThrowIfNull(exports);

            _Exports = new OpenNfsExportDefinition[exports.Count];
            int index = 0;

            foreach (OpenNfsExportDefinition? exportDefinition in exports)
            {
                if (exportDefinition is null)
                {
                    throw new ArgumentException("The export response cannot contain a null export definition.", nameof(exports));
                }

                _Exports[index++] = exportDefinition;
            }
        }

        /// <summary>
        /// Gets the exports exposed by the configured host.
        /// </summary>
        public IReadOnlyList<OpenNfsExportDefinition> Exports
        {
            get
            {
                return _Exports;
            }
        }
    }
}
