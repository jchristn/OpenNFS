namespace OpenNFS.XdrGen
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents a single validated XDR input to output-project mapping.
    /// </summary>
    public sealed class XdrProjectMapping
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrProjectMapping"/> class.
        /// </summary>
        /// <param name="id">Stable mapping identifier.</param>
        /// <param name="inputDirectory">Absolute input directory for XDR sources.</param>
        /// <param name="outputProjectPath">Absolute project path that owns the generated output.</param>
        /// <param name="outputNamespace">Generated namespace for the target output.</param>
        /// <param name="generatedDirectoryPath">Absolute generated directory path derived from the target project.</param>
        /// <param name="sourceFiles">Absolute vendored source files discovered for the mapping.</param>
        /// <param name="entryPointFiles">Absolute generation entry-point files for the mapping.</param>
        /// <exception cref="ArgumentException">Thrown when a text value is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required collection argument is null.</exception>
        public XdrProjectMapping(
            string id,
            string inputDirectory,
            string outputProjectPath,
            string outputNamespace,
            string generatedDirectoryPath,
            IReadOnlyList<string> sourceFiles,
            IReadOnlyList<string> entryPointFiles)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("The mapping identifier must contain a non-empty value.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(inputDirectory))
            {
                throw new ArgumentException("The input directory must contain a non-empty path.", nameof(inputDirectory));
            }

            if (string.IsNullOrWhiteSpace(outputProjectPath))
            {
                throw new ArgumentException("The output project path must contain a non-empty path.", nameof(outputProjectPath));
            }

            if (string.IsNullOrWhiteSpace(outputNamespace))
            {
                throw new ArgumentException("The output namespace must contain a non-empty value.", nameof(outputNamespace));
            }

            if (string.IsNullOrWhiteSpace(generatedDirectoryPath))
            {
                throw new ArgumentException("The generated directory path must contain a non-empty path.", nameof(generatedDirectoryPath));
            }

            ArgumentNullException.ThrowIfNull(sourceFiles);
            ArgumentNullException.ThrowIfNull(entryPointFiles);

            Id = id;
            InputDirectory = inputDirectory;
            OutputProjectPath = outputProjectPath;
            OutputNamespace = outputNamespace;
            GeneratedDirectoryPath = generatedDirectoryPath;
            SourceFiles = new ReadOnlyCollection<string>(new List<string>(sourceFiles));
            EntryPointFiles = new ReadOnlyCollection<string>(new List<string>(entryPointFiles));
        }

        /// <summary>
        /// Gets the stable mapping identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the absolute input directory for XDR sources.
        /// </summary>
        public string InputDirectory { get; }

        /// <summary>
        /// Gets the absolute project path that owns the generated output.
        /// </summary>
        public string OutputProjectPath { get; }

        /// <summary>
        /// Gets the generated namespace for the target output.
        /// </summary>
        public string OutputNamespace { get; }

        /// <summary>
        /// Gets the absolute generated directory path derived from the target project.
        /// </summary>
        public string GeneratedDirectoryPath { get; }

        /// <summary>
        /// Gets the absolute vendored source files discovered for the mapping.
        /// </summary>
        public IReadOnlyList<string> SourceFiles { get; }

        /// <summary>
        /// Gets the absolute XDR generation entry-point files for the mapping.
        /// </summary>
        public IReadOnlyList<string> EntryPointFiles { get; }
    }
}
