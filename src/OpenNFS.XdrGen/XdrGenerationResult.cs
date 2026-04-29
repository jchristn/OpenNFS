namespace OpenNFS.XdrGen
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using OpenNFS.XdrGen.Emission;
    using OpenNFS.XdrGen.Model;

    /// <summary>
    /// Result of executing the initial XDR generator manifest command.
    /// </summary>
    public sealed class XdrGenerationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrGenerationResult"/> class.
        /// </summary>
        /// <param name="configuration">Validated generator configuration.</param>
        /// <param name="checkOnly">True when the command validated only.</param>
        /// <param name="ensuredGeneratedDirectories">Absolute generated directories that were validated or materialized.</param>
        /// <param name="parsedDocuments">Parsed vendored XDR source documents.</param>
        /// <param name="generatedFiles">Generated C# file plans or emitted output file paths.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required reference argument is null.</exception>
        public XdrGenerationResult(
            XdrGeneratorConfiguration configuration,
            bool checkOnly,
            IReadOnlyList<string> ensuredGeneratedDirectories,
            IReadOnlyList<XdrDocument> parsedDocuments,
            IReadOnlyList<GeneratedCSharpFile> generatedFiles)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(ensuredGeneratedDirectories);
            ArgumentNullException.ThrowIfNull(parsedDocuments);
            ArgumentNullException.ThrowIfNull(generatedFiles);

            Configuration = configuration;
            CheckOnly = checkOnly;
            EnsuredGeneratedDirectories = new ReadOnlyCollection<string>(new List<string>(ensuredGeneratedDirectories));
            ParsedDocuments = new ReadOnlyCollection<XdrDocument>(new List<XdrDocument>(parsedDocuments));
            GeneratedFiles = new ReadOnlyCollection<GeneratedCSharpFile>(new List<GeneratedCSharpFile>(generatedFiles));
        }

        /// <summary>
        /// Gets the validated generator configuration.
        /// </summary>
        public XdrGeneratorConfiguration Configuration { get; }

        /// <summary>
        /// Gets a value indicating whether the command validated only.
        /// </summary>
        public bool CheckOnly { get; }

        /// <summary>
        /// Gets the absolute generated directories that were validated or materialized.
        /// </summary>
        public IReadOnlyList<string> EnsuredGeneratedDirectories { get; }

        /// <summary>
        /// Gets the parsed vendored XDR source documents.
        /// </summary>
        public IReadOnlyList<XdrDocument> ParsedDocuments { get; }

        /// <summary>
        /// Gets the generated C# files that were planned or emitted.
        /// </summary>
        public IReadOnlyList<GeneratedCSharpFile> GeneratedFiles { get; }
    }
}
