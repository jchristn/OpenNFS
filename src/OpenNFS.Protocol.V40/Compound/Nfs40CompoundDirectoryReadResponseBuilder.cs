namespace OpenNFS.Protocol.V40.Compound
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundDirectoryReadResponseBuilder
    {
        private readonly Nfs40CompoundDirectoryEntryEncoder _entryEncoder;

        internal Nfs40CompoundDirectoryReadResponseBuilder(OpenNfsServer server)
        {
            _entryEncoder = new Nfs40CompoundDirectoryEntryEncoder(server);
        }

        internal async Task<Nfs40DirectoryReadPayloadResult> TryBuildPayloadAsync(
            string exportPath,
            bitmap4? attributeRequest,
            IReadOnlyList<NfsDirectoryEntryInfo> directoryEntries,
            byte[] currentVerifier,
            ulong requestedCookie,
            uint maximumCount,
            CancellationToken cancellationToken)
        {
            READDIR4res emptyResult = CreateReadDirectorySuccessResult(
                currentVerifier,
                new List<entry4>(),
                eof: requestedCookie >= (ulong)directoryEntries.Count);
            if (maximumCount < Nfs40DirectoryListingSupport.MeasureReadDirectoryLength(emptyResult))
            {
                return new Nfs40DirectoryReadPayloadResult(nfsstat4.NFS4ERR_TOOSMALL, null);
            }

            List<entry4> selectedEntries = new List<entry4>();
            bool eof = true;
            bool appendedEntry = false;
            int startIndex = checked((int)requestedCookie);

            for (int index = startIndex; index < directoryEntries.Count; index++)
            {
                entry4 candidateEntry =
                    await _entryEncoder.CreateDirectoryEntryAsync(
                        exportPath,
                        attributeRequest,
                        directoryEntries[index],
                        (ulong)(index + 1),
                        cancellationToken).ConfigureAwait(false);
                selectedEntries.Add(candidateEntry);

                READDIR4res candidateResult = CreateReadDirectorySuccessResult(
                    currentVerifier,
                    selectedEntries,
                    eof: index + 1 >= directoryEntries.Count);

                if (Nfs40DirectoryListingSupport.MeasureReadDirectoryLength(candidateResult) > maximumCount)
                {
                    selectedEntries.RemoveAt(selectedEntries.Count - 1);
                    eof = false;

                    if (!appendedEntry)
                    {
                        return new Nfs40DirectoryReadPayloadResult(nfsstat4.NFS4ERR_TOOSMALL, null);
                    }

                    break;
                }

                appendedEntry = true;
            }

            if (requestedCookie >= (ulong)directoryEntries.Count)
            {
                eof = true;
            }

            READDIR4resok payload = new READDIR4resok
            {
                cookieverf = new verifier4
                {
                    Value = currentVerifier,
                },
                reply = new dirlist4
                {
                    entries = Nfs40DirectoryListingSupport.BuildEntryList(selectedEntries),
                    eof = eof,
                },
            };
            return new Nfs40DirectoryReadPayloadResult(nfsstat4.NFS4_OK, payload);
        }

        private static READDIR4res CreateReadDirectorySuccessResult(
            byte[] cookieVerifier,
            IReadOnlyList<entry4> entries,
            bool eof)
        {
            return new READDIR4res
            {
                status = nfsstat4.NFS4_OK,
                resok4 = new READDIR4resok
                {
                    cookieverf = new verifier4
                    {
                        Value = cookieVerifier,
                    },
                    reply = new dirlist4
                    {
                        entries = Nfs40DirectoryListingSupport.BuildEntryList(entries),
                        eof = eof,
                    },
                },
            };
        }
    }
}
