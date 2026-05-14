namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;
    using OpenNFS.Client;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V41.Backchannel;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Hosting;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V41.State;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV41SuiteSupport;

    /// <summary>
    /// Path-operation builder and mount-session routing NFSv4.1 suites.
    /// </summary>
    internal static class NfsV41PathOperationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "WireCompoundRejectsOperationsBeforeSequence",
                        displayName: "Wire-level COMPOUND surfaces NFS4ERR_OP_NOT_IN_SESSION when a non-session op precedes SEQUENCE",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs41CompoundService service = new Nfs41CompoundService(CreateProcessor());
                            COMPOUND4args misordered = new COMPOUND4args
                            {
                                tag = MakeTag("misordered"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
                                },
                            };
                            COMPOUND4res response = await DispatchCompoundAsync(service, misordered, "127.0.0.1:51004", xid: 30, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_OP_NOT_IN_SESSION)
                            {
                                throw new InvalidOperationException("A non-session op without preceding SEQUENCE must surface NFS4ERR_OP_NOT_IN_SESSION.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsGetAttributesCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + GETATTR COMPOUND op sequence with the requested attribute mask",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            bitmap4 mask = OpenNfsV41PathOperations.BuildAttributeMask(new ulong[]
                            {
                                Nfs41Constants.FATTR4_TYPE,
                                Nfs41Constants.FATTR4_SIZE,
                                Nfs41Constants.FATTR4_MODE,
                            });
                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildGetAttributesOps("/foo/bar/baz.txt", mask);

                            if (ops.Count != 5)
                            {
                                throw new InvalidOperationException("Expected PUTROOTFH + 3 LOOKUPs + GETATTR for a 3-component path. Observed op count: " + ops.Count);
                            }

                            if (ops[0].argop != nfs_opnum4.OP_PUTROOTFH)
                            {
                                throw new InvalidOperationException("First op must be PUTROOTFH. Observed: " + ops[0].argop);
                            }

                            string[] expectedComponents = new[] { "foo", "bar", "baz.txt" };
                            for (int index = 0; index < expectedComponents.Length; index++)
                            {
                                nfs_argop4 op = ops[index + 1];
                                if (op.argop != nfs_opnum4.OP_LOOKUP || op.oplookup?.objname?.Value?.Value?.Value is not byte[] actualBytes)
                                {
                                    throw new InvalidOperationException("Op at index " + (index + 1) + " must be a LOOKUP carrying a non-null objname.");
                                }

                                string actualText = System.Text.Encoding.UTF8.GetString(actualBytes);
                                if (!string.Equals(actualText, expectedComponents[index], StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException(
                                        "LOOKUP at index " + (index + 1) + " must carry component '"
                                        + expectedComponents[index] + "'. Observed: '" + actualText + "'");
                                }
                            }

                            nfs_argop4 terminal = ops[4];
                            if (terminal.argop != nfs_opnum4.OP_GETATTR
                                || terminal.opgetattr?.attr_request?.Value is not uint[] words
                                || !words.SequenceEqual(mask.Value!))
                            {
                                throw new InvalidOperationException("Terminal op must be GETATTR carrying the supplied attribute mask.");
                            }

                            // Empty / root path produces just PUTROOTFH + GETATTR.
                            IReadOnlyList<nfs_argop4> rootOps = OpenNfsV41PathOperations.BuildGetAttributesOps("/", mask);
                            if (rootOps.Count != 2
                                || rootOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootOps[1].argop != nfs_opnum4.OP_GETATTR)
                            {
                                throw new InvalidOperationException("Root path must produce exactly PUTROOTFH + GETATTR.");
                            }

                            // ".." segments must be rejected.
                            bool dotDotRejected = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildGetAttributesOps("/foo/../bar", mask);
                            }
                            catch (ArgumentException)
                            {
                                dotDotRejected = true;
                            }

                            _ = cancellationToken;

                            if (!dotDotRejected)
                            {
                                throw new InvalidOperationException("Path-first operations must reject '..' navigation segments.");
                            }

                            // The stat-like default mask must include FATTR4_TYPE, FATTR4_SIZE, FATTR4_MODE.
                            bitmap4 statMask = OpenNfsV41PathOperations.BuildStatLikeAttributeMask();
                            if (!IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_TYPE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_SIZE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_MODE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_OWNER))
                            {
                                throw new InvalidOperationException("BuildStatLikeAttributeMask must include FATTR4_TYPE, FATTR4_SIZE, FATTR4_MODE, and FATTR4_OWNER.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "MountSessionRoutesPathFirstGetAttributesThroughSession",
                        displayName: "OpenNfsV41MountSession.Metadata.GetAttributesAsync routes a path-first COMPOUND through the underlying session and surfaces a typed envelope",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 250, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41MountSession mountSession = session.CreateMountSession();
                            if (!ReferenceEquals(mountSession.Session, session))
                            {
                                throw new InvalidOperationException("OpenNfsV41MountSession.Session must reference the source session.");
                            }

                            OpenNfsV41CompoundResult result = await mountSession.Metadata
                                .GetAttributesAsync("/anywhere", cancellationToken)
                                .ConfigureAwait(false);

                            // Server's current v4.1 surface returns NFS4ERR_NOTSUPP for non-session ops.
                            // The mount-session facade must surface that as a partial-success envelope
                            // (SEQUENCE OK, terminal op carrying NOTSUPP) without flattening into a
                            // generic failure or hiding the partial state.
                            if (!result.ReachedServer)
                            {
                                throw new InvalidOperationException("Mount-session GETATTR must reach the server. Failure: " + result.Failure);
                            }

                            if (!result.HasPartialResults || result.Outcome is null)
                            {
                                throw new InvalidOperationException("Mount-session GETATTR against current v4.1 surface must surface partial results.");
                            }

                            // SEQUENCE was OK; the path-first ops surfaced NOTSUPP.
                            if (result.OperationsObservedSuccessfully < 1)
                            {
                                throw new InvalidOperationException("SEQUENCE inside the GETATTR COMPOUND must be observed as successful.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsReadCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + READ COMPOUND op sequence with the supplied stateid, offset, and count",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            stateid4 stateid = new stateid4
                            {
                                seqid = 7,
                                other = new byte[12] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
                            };

                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReadOps(
                                "/data/file.bin",
                                stateid,
                                offset: 4096,
                                count: 8192);

                            if (ops.Count != 4
                                || ops[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || ops[1].argop != nfs_opnum4.OP_LOOKUP
                                || ops[2].argop != nfs_opnum4.OP_LOOKUP
                                || ops[3].argop != nfs_opnum4.OP_READ)
                            {
                                throw new InvalidOperationException("BuildReadOps must produce PUTROOTFH + 2 LOOKUPs + READ for /data/file.bin.");
                            }

                            READ4args? readArgs = ops[3].opread;
                            if (readArgs?.stateid is null
                                || readArgs.stateid.seqid != 7
                                || readArgs.stateid.other is not byte[] otherBytes
                                || otherBytes.Length != 12
                                || readArgs.offset?.Value != 4096
                                || readArgs.count?.Value != 8192)
                            {
                                throw new InvalidOperationException("READ args must carry the supplied stateid (seqid 7 + 12-byte other), offset 4096, and count 8192.");
                            }

                            // Root path must produce just PUTROOTFH + READ (server will return ISDIR).
                            IReadOnlyList<nfs_argop4> rootReadOps = OpenNfsV41PathOperations.BuildReadOps(
                                string.Empty,
                                stateid,
                                offset: 0,
                                count: 1);
                            if (rootReadOps.Count != 2
                                || rootReadOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootReadOps[1].argop != nfs_opnum4.OP_READ)
                            {
                                throw new InvalidOperationException("Empty path with BuildReadOps must produce exactly PUTROOTFH + READ.");
                            }

                            // Null stateid must throw.
                            bool stateidGuardFired = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildReadOps("/x", stateid: null!, offset: 0, count: 0);
                            }
                            catch (ArgumentNullException)
                            {
                                stateidGuardFired = true;
                            }

                            if (!stateidGuardFired)
                            {
                                throw new InvalidOperationException("BuildReadOps must reject a null stateid argument.");
                            }

                            _ = cancellationToken;
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsReaddirCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + READDIR COMPOUND op sequence with the supplied cookie, verifier, and attribute mask",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            byte[] cookieVerifier = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };
                            bitmap4 mask = OpenNfsV41PathOperations.BuildStatLikeAttributeMask();

                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReaddirOps(
                                "/dir",
                                cookie: 1024,
                                cookieVerifier: cookieVerifier,
                                dircount: 4096,
                                maxcount: 8192,
                                attributeMask: mask);

                            if (ops.Count != 3
                                || ops[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || ops[1].argop != nfs_opnum4.OP_LOOKUP
                                || ops[2].argop != nfs_opnum4.OP_READDIR)
                            {
                                throw new InvalidOperationException("BuildReaddirOps must produce PUTROOTFH + 1 LOOKUP + READDIR for /dir.");
                            }

                            READDIR4args? readdirArgs = ops[2].opreaddir;
                            if (readdirArgs?.cookie?.Value != 1024
                                || readdirArgs.cookieverf?.Value is not byte[] verifierBytes
                                || !verifierBytes.SequenceEqual(cookieVerifier)
                                || readdirArgs.dircount?.Value != 4096
                                || readdirArgs.maxcount?.Value != 8192
                                || readdirArgs.attr_request is null
                                || readdirArgs.attr_request.Value is not uint[] attrWords
                                || !attrWords.SequenceEqual(mask.Value!))
                            {
                                throw new InvalidOperationException("READDIR args must carry cookie 1024, the supplied 8-byte verifier, dircount 4096, maxcount 8192, and the supplied attribute mask.");
                            }

                            // The verifier must be defensively copied: mutating the caller's array
                            // must not change the produced op.
                            cookieVerifier[0] = 0x00;
                            if (readdirArgs.cookieverf!.Value is not byte[] preservedBytes
                                || preservedBytes[0] != 0xAA)
                            {
                                throw new InvalidOperationException("READDIR cookie verifier must be defensively copied so caller mutation cannot tamper with the produced op.");
                            }

                            // A non-8-byte verifier must be rejected.
                            bool verifierLengthGuardFired = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildReaddirOps(
                                    "/dir",
                                    cookie: 0,
                                    cookieVerifier: new byte[7],
                                    dircount: 1,
                                    maxcount: 1,
                                    attributeMask: mask);
                            }
                            catch (ArgumentException)
                            {
                                verifierLengthGuardFired = true;
                            }

                            if (!verifierLengthGuardFired)
                            {
                                throw new InvalidOperationException("BuildReaddirOps must reject a cookie verifier whose length is not exactly 8 bytes.");
                            }

                            // Root path must produce just PUTROOTFH + READDIR.
                            IReadOnlyList<nfs_argop4> rootOps = OpenNfsV41PathOperations.BuildReaddirOps(
                                string.Empty,
                                cookie: 0,
                                cookieVerifier: new byte[8],
                                dircount: 1,
                                maxcount: 1,
                                attributeMask: mask);
                            if (rootOps.Count != 2
                                || rootOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootOps[1].argop != nfs_opnum4.OP_READDIR)
                            {
                                throw new InvalidOperationException("Empty path with BuildReaddirOps must produce exactly PUTROOTFH + READDIR.");
                            }

                            _ = cancellationToken;
                            return Task.CompletedTask;
                        }),
            };
        }
    }
}
