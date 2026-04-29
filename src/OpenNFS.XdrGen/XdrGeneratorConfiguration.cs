namespace OpenNFS.XdrGen
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents a validated XDR generator manifest.
    /// </summary>
    public sealed class XdrGeneratorConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrGeneratorConfiguration"/> class.
        /// </summary>
        /// <param name="configurationPath">Absolute path to the manifest file that was loaded.</param>
        /// <param name="projects">Validated project mappings.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="configurationPath"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="projects"/> is null.</exception>
        public XdrGeneratorConfiguration(string configurationPath, IReadOnlyList<XdrProjectMapping> projects)
        {
            if (string.IsNullOrWhiteSpace(configurationPath))
            {
                throw new ArgumentException("The configuration path must contain a non-empty path.", nameof(configurationPath));
            }

            ArgumentNullException.ThrowIfNull(projects);

            ConfigurationPath = configurationPath;
            Projects = new ReadOnlyCollection<XdrProjectMapping>(new List<XdrProjectMapping>(projects));
        }

        /// <summary>
        /// Gets the absolute path to the manifest file that was loaded.
        /// </summary>
        public string ConfigurationPath { get; }

        /// <summary>
        /// Gets the validated project mappings described by the manifest.
        /// </summary>
        public IReadOnlyList<XdrProjectMapping> Projects { get; }
    }
}

