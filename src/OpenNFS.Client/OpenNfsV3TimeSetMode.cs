namespace OpenNFS.Client
{
    /// <summary>
    /// Describes how an NFSv3 <c>SETATTR</c> request updates an access or modification timestamp.
    /// Mirrors the RFC 1813 <c>time_how</c> discriminant.
    /// </summary>
    public enum OpenNfsV3TimeSetMode
    {
        /// <summary>
        /// Leave the timestamp unchanged (<c>DONT_CHANGE</c>).
        /// </summary>
        DoNotChange = 0,

        /// <summary>
        /// Set the timestamp to the server's current time (<c>SET_TO_SERVER_TIME</c>).
        /// </summary>
        SetToServerTime = 1,

        /// <summary>
        /// Set the timestamp to a client-supplied value (<c>SET_TO_CLIENT_TIME</c>).
        /// </summary>
        SetToClientTime = 2,
    }
}
