namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Protocol.V42.Server.Operations;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering RFC 7862 NFSv4.2 sparse-file operations: SEEK, ALLOCATE, DEALLOCATE,
    /// and READ_PLUS.
    /// </summary>
    /// <remarks>
    /// These cases exercise the typed-level processor over the public <see cref="INfsSparse"/> capability
    /// seam. The wire-level COMPOUND-over-RPC dispatcher for v4.2 builds on the typed processor and
    /// arrives in a follow-on slice.
    /// </remarks>
    public static class NfsV42Suites
    {
        /// <summary>
        /// Creates the shared NFSv4.2 sparse-file suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NfsV42Suites",
                displayName: "NFSv4.2 Sparse File Surface",
                cases: new List<TestCaseDescriptor>
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

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "CopyWithinServer",
                        displayName: "COPY routes a same-server byte-range copy through the host capability and surfaces NFS4ERR_NOTSUPP without it",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            FakeCopyCloneHost host = new FakeCopyCloneHost();
                            Nfs42CopyCloneProcessor processor = new Nfs42CopyCloneProcessor(host);

                            COPY4args copyArgs = new COPY4args
                            {
                                ca_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                ca_dst_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                ca_src_offset = new offset4 { Value = 100 },
                                ca_dst_offset = new offset4 { Value = 200 },
                                ca_count = new length4 { Value = 4096 },
                                ca_consecutive = false,
                                ca_synchronous = true,
                                ca_source_server = Array.Empty<netloc4>(),
                            };

                            COPY4res result = await processor
                                .ProcessCopyAsync(copyArgs, "C:/exports/src.bin", "C:/exports/dst.bin")
                                .ConfigureAwait(false);

                            if (result.cr_status != nfsstat4.NFS4_OK
                                || result.cr_resok4 is null
                                || result.cr_resok4.cr_response?.wr_count?.Value != 4096
                                || result.cr_resok4.cr_response?.wr_committed != stable_how4.FILE_SYNC4
                                || host.CopyRequests.Count != 1
                                || host.CopyRequests[0].SourceOffset != 100
                                || host.CopyRequests[0].DestinationOffset != 200
                                || host.CopyRequests[0].Count != 4096
                                || !host.CopyRequests[0].Synchronous)
                            {
                                throw new InvalidOperationException("COPY must reach the host with the requested byte range and surface a stable-storage write response.");
                            }

                            COPY4args crossServerArgs = new COPY4args
                            {
                                ca_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                ca_dst_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                ca_src_offset = new offset4 { Value = 0 },
                                ca_dst_offset = new offset4 { Value = 0 },
                                ca_count = new length4 { Value = 1 },
                                ca_consecutive = false,
                                ca_synchronous = true,
                                ca_source_server = new[] { new netloc4 { nl_type = netloc_type4.NL4_NETADDR } },
                            };
                            COPY4res crossServerResult = await processor
                                .ProcessCopyAsync(crossServerArgs, "C:/exports/src.bin", "C:/exports/dst.bin")
                                .ConfigureAwait(false);
                            if (crossServerResult.cr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("Cross-server COPY must surface NFS4ERR_NOTSUPP because inter-server copy is not advertised.");
                            }

                            Nfs42CopyCloneProcessor unsupported = new Nfs42CopyCloneProcessor(copyCloneHost: null);
                            COPY4res unsupportedResult = await unsupported
                                .ProcessCopyAsync(copyArgs, "C:/exports/src.bin", "C:/exports/dst.bin")
                                .ConfigureAwait(false);
                            if (unsupportedResult.cr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("COPY without copy-clone capability must surface NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "IoAdviseAcknowledgesNoHints",
                        displayName: "IO_ADVISE accepts a hint bitmap and returns an empty acknowledged-hints set, per RFC 7862 §15.5",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            Nfs42AdvisoryOperationsProcessor processor = new Nfs42AdvisoryOperationsProcessor();
                            IO_ADVISE4res result = await processor.ProcessIoAdviseAsync(new IO_ADVISE4args
                            {
                                iaa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                iaa_offset = new offset4 { Value = 0 },
                                iaa_count = new length4 { Value = 4096 },
                                iaa_hints = new bitmap4 { Value = new uint[] { 0x07 } },
                            }).ConfigureAwait(false);

                            if (result.ior_status != nfsstat4.NFS4_OK
                                || result.resok4 is null
                                || result.resok4.ior_hints is null)
                            {
                                throw new InvalidOperationException("IO_ADVISE must succeed with a (possibly empty) acknowledged-hints bitmap.");
                            }

                            IO_ADVISE4res invalidResult = await processor.ProcessIoAdviseAsync(new IO_ADVISE4args
                            {
                                iaa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                iaa_offset = null,
                                iaa_count = null,
                                iaa_hints = null,
                            }).ConfigureAwait(false);
                            if (invalidResult.ior_status != nfsstat4.NFS4ERR_INVAL)
                            {
                                throw new InvalidOperationException("IO_ADVISE with missing required fields must surface NFS4ERR_INVAL.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "OffloadCancelAndStatusReturnNotSupported",
                        displayName: "OFFLOAD_CANCEL and OFFLOAD_STATUS surface NFS4ERR_NOTSUPP because asynchronous COPY is not advertised",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            Nfs42AdvisoryOperationsProcessor processor = new Nfs42AdvisoryOperationsProcessor();
                            OFFLOAD_CANCEL4res cancelResult = await processor
                                .ProcessOffloadCancelAsync(new OFFLOAD_CANCEL4args
                                {
                                    oca_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                })
                                .ConfigureAwait(false);
                            if (cancelResult.ocr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("OFFLOAD_CANCEL must surface NFS4ERR_NOTSUPP.");
                            }

                            OFFLOAD_STATUS4res statusResult = await processor
                                .ProcessOffloadStatusAsync(new OFFLOAD_STATUS4args
                                {
                                    osa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                })
                                .ConfigureAwait(false);
                            if (statusResult.osr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("OFFLOAD_STATUS must surface NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "WriteSameReturnsNotSupported",
                        displayName: "WRITE_SAME surfaces NFS4ERR_NOTSUPP because the current server does not advertise it",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            Nfs42AdvisoryOperationsProcessor processor = new Nfs42AdvisoryOperationsProcessor();
                            WRITE_SAME4res result = await processor
                                .ProcessWriteSameAsync(new WRITE_SAME4args
                                {
                                    wsa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    wsa_stable = stable_how4.UNSTABLE4,
                                    wsa_adb = new app_data_block4
                                    {
                                        adb_offset = new offset4 { Value = 0 },
                                        adb_block_size = new length4 { Value = 4096 },
                                        adb_block_count = new length4 { Value = 1 },
                                        adb_reloff_blocknum = new length4 { Value = 0 },
                                        adb_block_num = new count4 { Value = 0 },
                                        adb_reloff_pattern = new length4 { Value = 0 },
                                        adb_pattern = Array.Empty<byte>(),
                                    },
                                })
                                .ConfigureAwait(false);

                            if (result.wsr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("WRITE_SAME must surface NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "CopyNotifyReturnsNotSupportedForCrossServer",
                        displayName: "COPY_NOTIFY surfaces NFS4ERR_NOTSUPP because cross-server copy is not advertised",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            Nfs42AdvisoryOperationsProcessor processor = new Nfs42AdvisoryOperationsProcessor();
                            COPY_NOTIFY4res result = await processor
                                .ProcessCopyNotifyAsync(new COPY_NOTIFY4args
                                {
                                    cna_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    cna_destination_server = new netloc4
                                    {
                                        nl_type = netloc_type4.NL4_NETADDR,
                                    },
                                })
                                .ConfigureAwait(false);

                            if (result.cnr_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("COPY_NOTIFY must surface NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "AllAdvertisedOpsBound",
                        displayName: "Every advertised NFSv4.2 operation has a real typed handler binding that returns a non-null status",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            FakeSparseHost sparseHost = FakeSparseHost.Build(new[]
                            {
                                NfsSparseExtent.ForData(0, new byte[] { 0xFF }),
                            });
                            FakeCopyCloneHostBindCheck copyCloneHost = new FakeCopyCloneHostBindCheck();
                            Nfs42SparseFileProcessor sparseProcessor = new Nfs42SparseFileProcessor(sparseHost);
                            Nfs42CopyCloneProcessor copyCloneProcessor = new Nfs42CopyCloneProcessor(copyCloneHost);
                            Nfs42AdvisoryOperationsProcessor advisoryProcessor = new Nfs42AdvisoryOperationsProcessor();

                            // OP_ALLOCATE, OP_COPY, OP_COPY_NOTIFY, OP_DEALLOCATE, OP_IO_ADVISE, OP_OFFLOAD_CANCEL,
                            // OP_OFFLOAD_STATUS, OP_READ_PLUS, OP_SEEK, OP_WRITE_SAME, OP_CLONE — every v4.2
                            // op in scope (pNFS opnum slots are out of scope for this project).

                            ALLOCATE4res allocate = await sparseProcessor.ProcessAllocateAsync(
                                new ALLOCATE4args { aa_stateid = new stateid4 { seqid = 1, other = new byte[12] }, aa_offset = new offset4 { Value = 0 }, aa_length = new length4 { Value = 1 } },
                                "C:/exports/file.bin").ConfigureAwait(false);
                            EnsureBound(allocate.ar_status, "ALLOCATE");

                            COPY4res copy = await copyCloneProcessor.ProcessCopyAsync(
                                new COPY4args
                                {
                                    ca_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    ca_dst_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    ca_src_offset = new offset4 { Value = 0 },
                                    ca_dst_offset = new offset4 { Value = 0 },
                                    ca_count = new length4 { Value = 1 },
                                    ca_consecutive = false,
                                    ca_synchronous = true,
                                    ca_source_server = Array.Empty<netloc4>(),
                                },
                                "C:/exports/src.bin",
                                "C:/exports/dst.bin").ConfigureAwait(false);
                            EnsureBound(copy.cr_status, "COPY");

                            COPY_NOTIFY4res copyNotify = await advisoryProcessor.ProcessCopyNotifyAsync(
                                new COPY_NOTIFY4args
                                {
                                    cna_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    cna_destination_server = new netloc4 { nl_type = netloc_type4.NL4_NETADDR },
                                }).ConfigureAwait(false);
                            EnsureBound(copyNotify.cnr_status, "COPY_NOTIFY");

                            DEALLOCATE4res deallocate = await sparseProcessor.ProcessDeallocateAsync(
                                new DEALLOCATE4args { da_stateid = new stateid4 { seqid = 1, other = new byte[12] }, da_offset = new offset4 { Value = 0 }, da_length = new length4 { Value = 1 } },
                                "C:/exports/file.bin").ConfigureAwait(false);
                            EnsureBound(deallocate.dr_status, "DEALLOCATE");

                            IO_ADVISE4res ioAdvise = await advisoryProcessor.ProcessIoAdviseAsync(
                                new IO_ADVISE4args
                                {
                                    iaa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    iaa_offset = new offset4 { Value = 0 },
                                    iaa_count = new length4 { Value = 1 },
                                    iaa_hints = new bitmap4 { Value = Array.Empty<uint>() },
                                }).ConfigureAwait(false);
                            EnsureBound(ioAdvise.ior_status, "IO_ADVISE");

                            OFFLOAD_CANCEL4res offloadCancel = await advisoryProcessor.ProcessOffloadCancelAsync(
                                new OFFLOAD_CANCEL4args { oca_stateid = new stateid4 { seqid = 1, other = new byte[12] } }).ConfigureAwait(false);
                            EnsureBound(offloadCancel.ocr_status, "OFFLOAD_CANCEL");

                            OFFLOAD_STATUS4res offloadStatus = await advisoryProcessor.ProcessOffloadStatusAsync(
                                new OFFLOAD_STATUS4args { osa_stateid = new stateid4 { seqid = 1, other = new byte[12] } }).ConfigureAwait(false);
                            EnsureBound(offloadStatus.osr_status, "OFFLOAD_STATUS");

                            READ_PLUS4res readPlus = await sparseProcessor.ProcessReadPlusAsync(
                                new READ_PLUS4args { rpa_stateid = new stateid4 { seqid = 1, other = new byte[12] }, rpa_offset = new offset4 { Value = 0 }, rpa_count = new count4 { Value = 1 } },
                                "C:/exports/file.bin").ConfigureAwait(false);
                            EnsureBound(readPlus.rp_status, "READ_PLUS");

                            SEEK4res seek = await sparseProcessor.ProcessSeekAsync(
                                new SEEK4args { sa_stateid = new stateid4 { seqid = 1, other = new byte[12] }, sa_offset = new offset4 { Value = 0 }, sa_what = data_content4.NFS4_CONTENT_DATA },
                                "C:/exports/file.bin").ConfigureAwait(false);
                            EnsureBound(seek.sa_status, "SEEK");

                            WRITE_SAME4res writeSame = await advisoryProcessor.ProcessWriteSameAsync(
                                new WRITE_SAME4args
                                {
                                    wsa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    wsa_stable = stable_how4.UNSTABLE4,
                                    wsa_adb = new app_data_block4
                                    {
                                        adb_offset = new offset4 { Value = 0 },
                                        adb_block_size = new length4 { Value = 1 },
                                        adb_block_count = new length4 { Value = 1 },
                                        adb_reloff_blocknum = new length4 { Value = 0 },
                                        adb_block_num = new count4 { Value = 0 },
                                        adb_reloff_pattern = new length4 { Value = 0 },
                                        adb_pattern = Array.Empty<byte>(),
                                    },
                                }).ConfigureAwait(false);
                            EnsureBound(writeSame.wsr_status, "WRITE_SAME");

                            CLONE4res clone = await copyCloneProcessor.ProcessCloneAsync(
                                new CLONE4args
                                {
                                    cl_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    cl_dst_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    cl_src_offset = new offset4 { Value = 0 },
                                    cl_dst_offset = new offset4 { Value = 0 },
                                    cl_count = new length4 { Value = 1 },
                                },
                                "C:/exports/src.bin",
                                "C:/exports/dst.bin").ConfigureAwait(false);
                            EnsureBound(clone.cl_status, "CLONE");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV42Suites",
                        caseId: "CloneRange",
                        displayName: "CLONE routes a byte-range clone through the host capability and surfaces NFS4ERR_NOTSUPP without it",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            FakeCopyCloneHost host = new FakeCopyCloneHost();
                            Nfs42CopyCloneProcessor processor = new Nfs42CopyCloneProcessor(host);

                            CLONE4args cloneArgs = new CLONE4args
                            {
                                cl_src_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                cl_dst_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                cl_src_offset = new offset4 { Value = 0 },
                                cl_dst_offset = new offset4 { Value = 8192 },
                                cl_count = new length4 { Value = 65536 },
                            };

                            CLONE4res result = await processor
                                .ProcessCloneAsync(cloneArgs, "C:/exports/src.bin", "C:/exports/dst.bin")
                                .ConfigureAwait(false);

                            if (result.cl_status != nfsstat4.NFS4_OK
                                || host.CloneRequests.Count != 1
                                || host.CloneRequests[0].SourceOffset != 0
                                || host.CloneRequests[0].DestinationOffset != 8192
                                || host.CloneRequests[0].Count != 65536)
                            {
                                throw new InvalidOperationException("CLONE must reach the host with the requested byte range.");
                            }

                            Nfs42CopyCloneProcessor unsupported = new Nfs42CopyCloneProcessor(copyCloneHost: null);
                            CLONE4res unsupportedResult = await unsupported
                                .ProcessCloneAsync(cloneArgs, "C:/exports/src.bin", "C:/exports/dst.bin")
                                .ConfigureAwait(false);
                            if (unsupportedResult.cl_status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException("CLONE without copy-clone capability must surface NFS4ERR_NOTSUPP.");
                            }
                        }),
                });
        }

        private static void EnsureBound(nfsstat4? status, string operationName)
        {
            if (status is null)
            {
                throw new InvalidOperationException(
                    "Operation " + operationName + " returned a null status; every advertised v4.2 op must produce a real status.");
            }
        }

        private sealed class FakeCopyCloneHostBindCheck : INfsCopyClone
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

        private sealed class FakeCopyCloneHost : INfsCopyClone
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

        private static SEEK4args BuildSeek(uint stateidSeqid, ulong offset, data_content4 what)
        {
            return new SEEK4args
            {
                sa_stateid = new stateid4 { seqid = stateidSeqid, other = new byte[12] },
                sa_offset = new offset4 { Value = offset },
                sa_what = what,
            };
        }

        private static void EnsureSeekResult(SEEK4res result, ulong expectedOffset, bool expectedEof)
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

        private sealed class FakeSparseHost : INfsSparse
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
