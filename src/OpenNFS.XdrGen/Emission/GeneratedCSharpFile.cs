namespace OpenNFS.XdrGen.Emission
{
    using System;

    /// <summary>
    /// Represents one generated C# file emitted from XDR input.
    /// </summary>
    public sealed class GeneratedCSharpFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedCSharpFile"/> class.
        /// </summary>
        /// <param name="filePath">Absolute target file path.</param>
        /// <param name="typeName">Primary generated type name.</param>
        /// <param name="sourceText">Generated source text.</param>
        public GeneratedCSharpFile(string filePath, string typeName, string sourceText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            ArgumentNullException.ThrowIfNull(sourceText);

            FilePath = filePath;
            TypeName = typeName;
            SourceText = sourceText;
        }

        /// <summary>
        /// Gets the absolute target file path.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the primary generated type name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the generated source text.
        /// </summary>
        public string SourceText { get; }
    }
}
