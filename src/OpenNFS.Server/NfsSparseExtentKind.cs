namespace OpenNFS.Server
{
    /// <summary>
    /// Identifies the kind of extent returned by the sparse-file <c>ReadSparseAsync</c> path.
    /// </summary>
    public enum NfsSparseExtentKind
    {
        /// <summary>
        /// The extent contains real data bytes.
        /// </summary>
        Data = 0,

        /// <summary>
        /// The extent is a hole, with no backing storage and an implied zero-fill.
        /// </summary>
        Hole = 1,
    }
}
