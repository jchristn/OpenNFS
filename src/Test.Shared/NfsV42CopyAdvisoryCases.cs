namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Protocol.V42.Server.Operations;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV42SuiteSupport;

    /// <summary>
    /// NFSv4.2 copy, clone, advisory, and binding suites.
    /// </summary>
    internal static class NfsV42CopyAdvisoryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
                    displayName: "IO_ADVISE accepts a hint bitmap and returns an empty acknowledged-hints set, per RFC 7862 Â§15.5",
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
            };
        }
    }
}
