#pragma warning disable CS1591

namespace OpenNFS.XdrGen.Parsing
{
    /// <summary>
    /// Token kinds recognized by the XDR tokenizer.
    /// </summary>
    public enum XdrTokenKind
    {
        Identifier,
        Number,
        Symbol,
        EndOfFile,
    }

    /// <summary>
    /// Tokenized XDR source element.
    /// </summary>
    /// <param name="Kind">Token kind.</param>
    /// <param name="Text">Token text.</param>
    /// <param name="Line">1-based line number.</param>
    /// <param name="Column">1-based column number.</param>
    public sealed record class XdrToken(XdrTokenKind Kind, string Text, int Line, int Column);
}

#pragma warning restore CS1591
