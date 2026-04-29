namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Decoded entry from a MOUNT v3 <c>EXPORT</c> reply.
    /// </summary>
    public sealed class OpenNfsExportV3Entry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsExportV3Entry"/> class.
        /// </summary>
        /// <param name="exportPath">Decoded export path.</param>
        /// <param name="authorizedClientGroups">
        /// Decoded host-group or netgroup names associated with the export.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="exportPath"/> is empty or whitespace.</exception>
        public OpenNfsExportV3Entry(string exportPath, IReadOnlyList<string>? authorizedClientGroups = null)
        {
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                throw new ArgumentException("The export path must contain a directory path.", nameof(exportPath));
            }

            ExportPath = exportPath;
            AuthorizedClientGroups = CopyGroups(authorizedClientGroups);
        }

        /// <summary>
        /// Gets the decoded export path.
        /// </summary>
        public string ExportPath { get; }

        /// <summary>
        /// Gets the decoded host-group or netgroup names associated with the export.
        /// </summary>
        public IReadOnlyList<string> AuthorizedClientGroups { get; }

        private static IReadOnlyList<string> CopyGroups(IReadOnlyList<string>? authorizedClientGroups)
        {
            if (authorizedClientGroups is null || authorizedClientGroups.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] copy = new string[authorizedClientGroups.Count];
            for (int index = 0; index < authorizedClientGroups.Count; index++)
            {
                string? group = authorizedClientGroups[index];
                if (string.IsNullOrWhiteSpace(group))
                {
                    throw new ArgumentException(
                        "Every authorized client group must contain a non-empty host-group or netgroup name.",
                        nameof(authorizedClientGroups));
                }

                copy[index] = group;
            }

            return copy;
        }
    }
}
