namespace OpenNFS.XdrGen
{
    using System;

    /// <summary>
    /// Options controlling XDR manifest validation and output-directory preparation.
    /// </summary>
    public sealed class XdrGeneratorOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrGeneratorOptions"/> class.
        /// </summary>
        /// <param name="configurationPath">Path to the generator manifest.</param>
        /// <param name="checkOnly">True to validate without creating output directories.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="configurationPath"/> is empty or whitespace.</exception>
        public XdrGeneratorOptions(string configurationPath, bool checkOnly = false)
        {
            if (string.IsNullOrWhiteSpace(configurationPath))
            {
                throw new ArgumentException("The XDR generator configuration path must contain a non-empty file path.", nameof(configurationPath));
            }

            ConfigurationPath = configurationPath;
            CheckOnly = checkOnly;
        }

        /// <summary>
        /// Gets the path to the generator manifest.
        /// </summary>
        public string ConfigurationPath { get; }

        /// <summary>
        /// Gets a value indicating whether the command should validate only.
        /// </summary>
        public bool CheckOnly { get; }
    }
}

