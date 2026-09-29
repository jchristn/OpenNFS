namespace OpenNFS.Client.Internal
{
    using System;

    /// <summary>
    /// Result of resolving a mounted-session path through successive NFSv3 <c>LOOKUP</c> calls.
    /// </summary>
    internal sealed class OpenNfsMountSessionResolution
    {
        internal OpenNfsMountSessionResolution(OpenNfsV3Status status, byte[] fileHandle, OpenNfsV3Attributes? attributes)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            Status = status;
            FileHandle = fileHandle;
            Attributes = attributes;
        }

        internal OpenNfsV3Status Status { get; }

        internal byte[] FileHandle { get; }

        internal OpenNfsV3Attributes? Attributes { get; }
    }
}
