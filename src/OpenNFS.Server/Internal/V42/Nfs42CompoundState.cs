namespace OpenNFS.Server.Internal.V42
{
    using System;
    using OpenNFS.Server;

    internal sealed class Nfs42ResolvedHandle
    {
        internal Nfs42ResolvedHandle(NfsFileHandle fileHandle, NfsFileHandleTarget target, NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(pathInfo);
            FileHandle = fileHandle;
            Target = target;
            PathInfo = pathInfo;
        }

        internal NfsFileHandle FileHandle { get; }

        internal NfsFileHandleTarget Target { get; }

        internal NfsPathInfo PathInfo { get; }
    }

    internal sealed class Nfs42CompoundState
    {
        private Nfs42ResolvedHandle? currentHandle;
        private Nfs42ResolvedHandle? savedHandle;

        internal void SetCurrentHandle(Nfs42ResolvedHandle handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            currentHandle = handle;
        }

        internal bool TryGetCurrentHandle(out Nfs42ResolvedHandle? handle)
        {
            handle = currentHandle;
            return handle is not null;
        }

        internal bool SaveCurrentHandle()
        {
            if (currentHandle is null)
            {
                return false;
            }

            savedHandle = currentHandle;
            return true;
        }

        internal bool TryGetSavedHandle(out Nfs42ResolvedHandle? handle)
        {
            handle = savedHandle;
            return handle is not null;
        }
    }
}
