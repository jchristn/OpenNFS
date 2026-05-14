namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal readonly struct ResolvedHandleStatus
    {
        internal ResolvedHandleStatus(Nfs40CompoundResolvedHandle? handle, nfsstat4 status)
        {
            Handle = handle;
            Status = status;
        }

        internal Nfs40CompoundResolvedHandle? Handle { get; }

        internal nfsstat4 Status { get; }
    }
}
