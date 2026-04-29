namespace OpenNFS.XdrGen.Parsing
{
    using System;

    /// <summary>
    /// Exception raised when XDR parsing fails.
    /// </summary>
    public sealed class XdrParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrParseException"/> class.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        /// <param name="line">1-based line number.</param>
        /// <param name="column">1-based column number.</param>
        /// <param name="message">Failure message.</param>
        public XdrParseException(string filePath, int line, int column, string message)
            : base(filePath + "(" + line + "," + column + "): " + message)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the file path in which parsing failed.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the 1-based line number at which parsing failed.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the 1-based column number at which parsing failed.
        /// </summary>
        public int Column { get; }
    }
}
