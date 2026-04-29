namespace OpenNFS.Server
{
    /// <summary>
    /// Describes the resolved kind of a host-local source path.
    /// </summary>
    public enum NfsPathKind
    {
        /// <summary>
        /// The path does not currently exist.
        /// </summary>
        Missing = 0,

        /// <summary>
        /// The path resolves to a directory.
        /// </summary>
        Directory = 1,

        /// <summary>
        /// The path resolves to a file.
        /// </summary>
        File = 2,

        /// <summary>
        /// The path resolves to a symbolic link.
        /// </summary>
        SymbolicLink = 3,

        /// <summary>
        /// The path resolves to another supported platform object.
        /// </summary>
        Other = 4,
    }
}
