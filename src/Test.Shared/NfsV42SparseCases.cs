namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Protocol.V42.Server.Operations;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV42SuiteSupport;

    /// <summary>
    /// NFSv4.2 sparse-file SEEK, ALLOCATE, DEALLOCATE, and READ_PLUS suites.
    /// </summary>
    internal static class NfsV42SparseCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV42Suites",
                    caseId: "SeekHoleAndData",
                    displayName: "SEEK locates the next data and hole boundaries in a sparse host",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        FakeSparseHost host = FakeSparseHost.Build(new[]
                        {
                            NfsSparseExtent.ForData(0, new byte[] { 0x01, 0x02, 0x03, 0x04 }),
                            NfsSparseExtent.ForHole(4, 8),
                            NfsSparseExtent.ForData(12, new byte[] { 0x05, 0x06 }),
                        });

                        Nfs42SparseFileProcessor processor = new Nfs42SparseFileProcessor(host);

                        SEEK4res nextDataFromZero = await processor
                            .ProcessSeekAsync(BuildSeek(stateidSeqid: 1, offset: 0, what: data_content4.NFS4_CONTENT_DATA), "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        EnsureSeekResult(nextDataFromZero, expectedOffset: 0, expectedEof: false);

                        SEEK4res nextHoleFromZero = await processor
                            .ProcessSeekAsync(BuildSeek(stateidSeqid: 1, offset: 0, what: data_content4.NFS4_CONTENT_HOLE), "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        EnsureSeekResult(nextHoleFromZero, expectedOffset: 4, expectedEof: false);

                        SEEK4res nextDataFromMidHole = await processor
                            .ProcessSeekAsync(BuildSeek(stateidSeqid: 1, offset: 6, what: data_content4.NFS4_CONTENT_DATA), "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        EnsureSeekResult(nextDataFromMidHole, expectedOffset: 12, expectedEof: false);

                        SEEK4res nextDataAtEndOfFile = await processor
                            .ProcessSeekAsync(BuildSeek(stateidSeqid: 1, offset: 14, what: data_content4.NFS4_CONTENT_DATA), "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        EnsureSeekResult(nextDataAtEndOfFile, expectedOffset: 14, expectedEof: true);
                    }),

                new TestCaseDescriptor(
                    suiteId: "NfsV42Suites",
                    caseId: "AllocateDeallocate",
                    displayName: "ALLOCATE and DEALLOCATE round-trip and surface NFS4ERR_NOTSUPP when the host has not opted in",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        FakeSparseHost host = FakeSparseHost.Build(new[]
                        {
                            NfsSparseExtent.ForHole(0, 1024),
                        });

                        Nfs42SparseFileProcessor processor = new Nfs42SparseFileProcessor(host);

                        ALLOCATE4res allocateResult = await processor
                            .ProcessAllocateAsync(
                                new ALLOCATE4args
                                {
                                    aa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    aa_offset = new offset4 { Value = 0 },
                                    aa_length = new length4 { Value = 256 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        if (allocateResult.ar_status != nfsstat4.NFS4_OK
                            || host.AllocateRequests.Count != 1
                            || host.AllocateRequests[0].Length != 256)
                        {
                            throw new InvalidOperationException("ALLOCATE must reach the host with the requested byte range.");
                        }

                        DEALLOCATE4res deallocateResult = await processor
                            .ProcessDeallocateAsync(
                                new DEALLOCATE4args
                                {
                                    da_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    da_offset = new offset4 { Value = 64 },
                                    da_length = new length4 { Value = 128 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        if (deallocateResult.dr_status != nfsstat4.NFS4_OK
                            || host.DeallocateRequests.Count != 1
                            || host.DeallocateRequests[0].Offset != 64
                            || host.DeallocateRequests[0].Length != 128)
                        {
                            throw new InvalidOperationException("DEALLOCATE must reach the host with the requested byte range.");
                        }

                        Nfs42SparseFileProcessor unsupportedProcessor = new Nfs42SparseFileProcessor(sparseHost: null);
                        ALLOCATE4res unsupportedAllocate = await unsupportedProcessor
                            .ProcessAllocateAsync(
                                new ALLOCATE4args
                                {
                                    aa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    aa_offset = new offset4 { Value = 0 },
                                    aa_length = new length4 { Value = 1 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        if (unsupportedAllocate.ar_status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("ALLOCATE without sparse capability must surface NFS4ERR_NOTSUPP.");
                        }

                        DEALLOCATE4res unsupportedDeallocate = await unsupportedProcessor
                            .ProcessDeallocateAsync(
                                new DEALLOCATE4args
                                {
                                    da_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    da_offset = new offset4 { Value = 0 },
                                    da_length = new length4 { Value = 1 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        if (unsupportedDeallocate.dr_status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("DEALLOCATE without sparse capability must surface NFS4ERR_NOTSUPP.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "NfsV42Suites",
                    caseId: "ReadPlusSparseRoundTrip",
                    displayName: "READ_PLUS returns alternating data and hole extents from the sparse host",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        byte[] firstChunk = new byte[] { 0xAA, 0xBB };
                        byte[] secondChunk = new byte[] { 0xCC, 0xDD, 0xEE };
                        FakeSparseHost host = FakeSparseHost.Build(new[]
                        {
                            NfsSparseExtent.ForData(0, firstChunk),
                            NfsSparseExtent.ForHole(2, 5),
                            NfsSparseExtent.ForData(7, secondChunk),
                        });

                        Nfs42SparseFileProcessor processor = new Nfs42SparseFileProcessor(host);

                        READ_PLUS4res result = await processor
                            .ProcessReadPlusAsync(
                                new READ_PLUS4args
                                {
                                    rpa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    rpa_offset = new offset4 { Value = 0 },
                                    rpa_count = new count4 { Value = 32 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);

                        if (result.rp_status != nfsstat4.NFS4_OK
                            || result.rp_resok4 is null
                            || result.rp_resok4.rpr_contents is null
                            || result.rp_resok4.rpr_contents.Length != 3)
                        {
                            throw new InvalidOperationException("READ_PLUS must return three extents.");
                        }

                        read_plus_content[] contents = result.rp_resok4.rpr_contents;

                        if (contents[0].rpc_content != data_content4.NFS4_CONTENT_DATA
                            || contents[0].rpc_data?.d_offset?.Value != 0
                            || !(contents[0].rpc_data?.d_data ?? Array.Empty<byte>()).SequenceEqual(firstChunk))
                        {
                            throw new InvalidOperationException("First extent must be a data extent at offset 0 carrying the first chunk.");
                        }

                        if (contents[1].rpc_content != data_content4.NFS4_CONTENT_HOLE
                            || contents[1].rpc_hole?.di_offset?.Value != 2
                            || contents[1].rpc_hole?.di_length?.Value != 5)
                        {
                            throw new InvalidOperationException("Second extent must be a hole at offset 2 of length 5.");
                        }

                        if (contents[2].rpc_content != data_content4.NFS4_CONTENT_DATA
                            || contents[2].rpc_data?.d_offset?.Value != 7
                            || !(contents[2].rpc_data?.d_data ?? Array.Empty<byte>()).SequenceEqual(secondChunk))
                        {
                            throw new InvalidOperationException("Third extent must be a data extent at offset 7 carrying the second chunk.");
                        }

                        if (!result.rp_resok4.rpr_eof)
                        {
                            throw new InvalidOperationException("READ_PLUS over the entire mock should report end-of-file.");
                        }

                        Nfs42SparseFileProcessor unsupportedProcessor = new Nfs42SparseFileProcessor(sparseHost: null);
                        READ_PLUS4res unsupported = await unsupportedProcessor
                            .ProcessReadPlusAsync(
                                new READ_PLUS4args
                                {
                                    rpa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    rpa_offset = new offset4 { Value = 0 },
                                    rpa_count = new count4 { Value = 1 },
                                },
                                "C:/exports/sparse.bin")
                            .ConfigureAwait(false);
                        if (unsupported.rp_status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("READ_PLUS without sparse capability must surface NFS4ERR_NOTSUPP.");
                        }
                    }),
            };
        }
    }
}
