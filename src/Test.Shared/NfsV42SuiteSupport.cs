namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Shared helpers for the NFSv4.2 sparse-file suite catalog.
    /// </summary>
    internal static class NfsV42SuiteSupport
    {
        internal static void EnsureBound(nfsstat4? status, string operationName)
        {
            if (status is null)
            {
                throw new InvalidOperationException(
                    "Operation " + operationName + " returned a null status; every advertised v4.2 op must produce a real status.");
            }
        }

        internal static SEEK4args BuildSeek(uint stateidSeqid, ulong offset, data_content4 what)
        {
            return new SEEK4args
            {
                sa_stateid = new stateid4 { seqid = stateidSeqid, other = new byte[12] },
                sa_offset = new offset4 { Value = offset },
                sa_what = what,
            };
        }

        internal static void EnsureSeekResult(SEEK4res result, ulong expectedOffset, bool expectedEof)
        {
            if (result.sa_status != nfsstat4.NFS4_OK
                || result.resok4 is null
                || result.resok4.sr_offset?.Value != expectedOffset
                || result.resok4.sr_eof != expectedEof)
            {
                throw new InvalidOperationException(
                    "SEEK result must be NFS4_OK with offset=" + expectedOffset + " eof=" + expectedEof
                    + ", but received status=" + result.sa_status?.ToString()
                    + " offset=" + result.resok4?.sr_offset?.Value
                    + " eof=" + result.resok4?.sr_eof + ".");
            }
        }

        internal sealed class FakeCopyCloneHostBindCheck : INfsCopyClone
        {
            public ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request)
            {
                return ValueTask.FromResult(new NfsCopyResponse(request.Count, committedToStableStorage: true));
            }

            public ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request)
            {
                return ValueTask.FromResult(NfsCloneResponse.Success);
            }
        }

        internal sealed class FakeCopyCloneHost : INfsCopyClone
        {
            private readonly List<NfsCopyRequest> copyRequests = new List<NfsCopyRequest>();
            private readonly List<NfsCloneRequest> cloneRequests = new List<NfsCloneRequest>();

            internal IReadOnlyList<NfsCopyRequest> CopyRequests => copyRequests;

            internal IReadOnlyList<NfsCloneRequest> CloneRequests => cloneRequests;

            public ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request)
            {
                copyRequests.Add(request);
                return ValueTask.FromResult(new NfsCopyResponse(request.Count, committedToStableStorage: true));
            }

            public ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request)
            {
                cloneRequests.Add(request);
                return ValueTask.FromResult(NfsCloneResponse.Success);
            }
        }

        internal sealed class FakeSparseHost : INfsSparse
        {
            private readonly IReadOnlyList<NfsSparseExtent> extents;
            private readonly ulong fileLength;
            private readonly List<NfsAllocateRequest> allocateRequests = new List<NfsAllocateRequest>();
            private readonly List<NfsDeallocateRequest> deallocateRequests = new List<NfsDeallocateRequest>();

            private FakeSparseHost(IReadOnlyList<NfsSparseExtent> extents, ulong fileLength)
            {
                this.extents = extents;
                this.fileLength = fileLength;
            }

            internal IReadOnlyList<NfsAllocateRequest> AllocateRequests => allocateRequests;

            internal IReadOnlyList<NfsDeallocateRequest> DeallocateRequests => deallocateRequests;

            internal static FakeSparseHost Build(IReadOnlyList<NfsSparseExtent> extents)
            {
                ulong totalLength = 0;
                foreach (NfsSparseExtent extent in extents)
                {
                    totalLength = Math.Max(totalLength, extent.Offset + extent.Length);
                }

                return new FakeSparseHost(extents, totalLength);
            }

            public ValueTask<NfsSeekResponse> SeekAsync(NfsSeekRequest request)
            {
                ulong start = request.Offset;
                if (start >= fileLength)
                {
                    return ValueTask.FromResult(new NfsSeekResponse(fileLength, true));
                }

                foreach (NfsSparseExtent extent in extents)
                {
                    ulong end = extent.Offset + extent.Length;
                    if (end <= start)
                    {
                        continue;
                    }

                    bool isData = extent.Kind == NfsSparseExtentKind.Data;
                    if (isData == request.SearchForData)
                    {
                        ulong matchOffset = Math.Max(start, extent.Offset);
                        return ValueTask.FromResult(new NfsSeekResponse(matchOffset, false));
                    }
                }

                return ValueTask.FromResult(new NfsSeekResponse(fileLength, true));
            }

            public ValueTask<NfsAllocateResponse> AllocateAsync(NfsAllocateRequest request)
            {
                allocateRequests.Add(request);
                return ValueTask.FromResult(NfsAllocateResponse.Success);
            }

            public ValueTask<NfsDeallocateResponse> DeallocateAsync(NfsDeallocateRequest request)
            {
                deallocateRequests.Add(request);
                return ValueTask.FromResult(NfsDeallocateResponse.Success);
            }

            public ValueTask<NfsReadSparseResponse> ReadSparseAsync(NfsReadSparseRequest request)
            {
                List<NfsSparseExtent> result = new List<NfsSparseExtent>();
                ulong end = request.Offset + request.Count;
                bool reachedEof = false;

                foreach (NfsSparseExtent extent in extents)
                {
                    ulong extentEnd = extent.Offset + extent.Length;
                    if (extentEnd <= request.Offset || extent.Offset >= end)
                    {
                        continue;
                    }

                    ulong sliceStart = Math.Max(request.Offset, extent.Offset);
                    ulong sliceEnd = Math.Min(end, extentEnd);
                    ulong sliceLength = sliceEnd - sliceStart;

                    if (extent.Kind == NfsSparseExtentKind.Data)
                    {
                        byte[] originalData = extent.GetData();
                        int sliceOffsetInExtent = (int)(sliceStart - extent.Offset);
                        byte[] sliceData = new byte[(int)sliceLength];
                        Buffer.BlockCopy(originalData, sliceOffsetInExtent, sliceData, 0, sliceData.Length);
                        result.Add(NfsSparseExtent.ForData(sliceStart, sliceData));
                    }
                    else
                    {
                        result.Add(NfsSparseExtent.ForHole(sliceStart, sliceLength));
                    }

                    if (extentEnd >= fileLength && extentEnd <= end)
                    {
                        reachedEof = true;
                    }
                }

                if (end >= fileLength)
                {
                    reachedEof = true;
                }

                return ValueTask.FromResult(new NfsReadSparseResponse(result, reachedEof));
            }
        }
    }
}
