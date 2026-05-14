namespace OpenNFS.Protocol.V40.Compound
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundDirectoryEntryEncoder
    {
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundDirectoryEntryEncoder(OpenNfsServer server)
        {
            _server = server;
        }

        internal async Task<entry4> CreateDirectoryEntryAsync(
            string exportPath,
            bitmap4? attributeRequest,
            NfsDirectoryEntryInfo directoryEntry,
            ulong cookie,
            CancellationToken cancellationToken)
        {
            NfsFileHandleTarget childTarget = new NfsFileHandleTarget(exportPath, directoryEntry.PathInfo.Path);
            NfsFileHandle childFileHandle =
                await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
            Nfs40CompoundResolvedHandle childHandle =
                new Nfs40CompoundResolvedHandle(childFileHandle, childTarget, directoryEntry.PathInfo);

            TryCreateAttributesResult attributeResult =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    childHandle,
                    attributeRequest,
                    cancellationToken).ConfigureAwait(false);
            if (attributeResult.Attributes is null)
            {
                throw new InvalidDataException(
                    "Unable to encode requested READDIR attributes due to status " + attributeResult.ErrorStatus.ToString() + ".");
            }

            fattr4 attributes = attributeResult.Attributes;
            return new entry4
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                name = new component4
                {
                    Value = new utf8str_cs
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(directoryEntry.Name),
                        },
                    },
                },
                attrs = attributes,
            };
        }
    }
}
