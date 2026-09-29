namespace OpenNFS.Client
{
    /// <summary>
    /// Typed NFSv3 <c>SETATTR</c> result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3SetAttributesResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV3SetAttributesResult"/> class.
        /// </summary>
        /// <param name="status">Returned NFSv3 status.</param>
        /// <param name="wcc">
        /// Weak-cache-consistency data for the target object, when the server supplied it.
        /// Default value: <c>null</c>.
        /// </param>
        public OpenNfsV3SetAttributesResult(OpenNfsV3Status status, OpenNfsV3WeakCacheConsistency? wcc = null)
        {
            Status = status;
            Wcc = wcc;
        }

        /// <summary>
        /// Gets the returned NFSv3 status.
        /// </summary>
        public OpenNfsV3Status Status { get; }

        /// <summary>
        /// Gets a value indicating whether the server applied the requested attribute changes.
        /// </summary>
        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        /// <summary>
        /// Gets the weak-cache-consistency data (pre-operation and post-operation attributes) for the target object.
        /// </summary>
        public OpenNfsV3WeakCacheConsistency? Wcc { get; }
    }
}
