namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40CompoundOperationResult
    {
        internal Nfs40CompoundOperationResult(nfsstat4 status, nfs_resop4 responseOperation)
        {
            ArgumentNullException.ThrowIfNull(responseOperation);
            Status = status;
            ResponseOperation = responseOperation;
        }

        internal nfs_resop4 ResponseOperation { get; }

        internal nfsstat4 Status { get; }
    }
}
