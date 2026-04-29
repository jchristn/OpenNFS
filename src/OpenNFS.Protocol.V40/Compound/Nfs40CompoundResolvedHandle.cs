namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundResolvedHandle
    {
        internal Nfs40CompoundResolvedHandle(
            NfsFileHandle fileHandle,
            NfsFileHandleTarget target,
            NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(pathInfo);

            FileHandle = fileHandle;
            Target = target;
            PathInfo = pathInfo;
        }

        internal NfsFileHandle FileHandle { get; }

        internal NfsPathInfo PathInfo { get; }

        internal NfsFileHandleTarget Target { get; }
    }
}
