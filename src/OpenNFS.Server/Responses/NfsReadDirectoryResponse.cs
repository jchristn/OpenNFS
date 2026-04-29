namespace OpenNFS.Server.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response context containing the child entries discovered beneath a host-local directory path.
    /// </summary>
    public sealed class NfsReadDirectoryResponse
    {
        private readonly NfsDirectoryEntryInfo[] _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadDirectoryResponse"/> class.
        /// </summary>
        /// <param name="entries">Ordered child entries discovered beneath the requested directory path.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="entries"/> contains a null entry.</exception>
        public NfsReadDirectoryResponse(IReadOnlyCollection<NfsDirectoryEntryInfo> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            _entries = new NfsDirectoryEntryInfo[entries.Count];
            int index = 0;

            foreach (NfsDirectoryEntryInfo? entry in entries)
            {
                if (entry is null)
                {
                    throw new ArgumentException("The directory-entry collection cannot contain null entries.", nameof(entries));
                }

                _entries[index++] = entry;
            }
        }

        /// <summary>
        /// Gets the ordered child entries discovered beneath the requested directory path.
        /// </summary>
        public IReadOnlyList<NfsDirectoryEntryInfo> Entries
        {
            get
            {
                return _entries;
            }
        }
    }
}
