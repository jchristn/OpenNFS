namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40DirectoryReadPayloadResult
    {
        internal Nfs40DirectoryReadPayloadResult(
            nfsstat4 status,
            READDIR4resok? payload)
        {
            Status = status;
            Payload = payload;
        }

        internal READDIR4resok? Payload { get; }

        internal nfsstat4 Status { get; }
    }
}
