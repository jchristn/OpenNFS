namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientV40SuiteSupport;

    /// <summary>
    /// Raw NFSv4.0 client transport and explicit compound operation suites.
    /// </summary>
    internal static class ClientV40RawCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "RawCompoundExecutionPositive",
                        displayName: "Raw NFSv4.0 COMPOUND execution succeeds over the public client transport path",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();

                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 1,
                                async client =>
                                {
                                    OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-positive",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutFileHandleOperation(rootHandle.ToArray()),
                                                CreateReadDirectoryOperation(0UL, new byte[8], 4096U),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    if (reply.Plan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || !string.Equals(reply.OperationName, "Raw NFSv4.0 COMPOUND", StringComparison.Ordinal))
                                    {
                                        throw new InvalidOperationException("Expected raw NFSv4.0 execution to preserve the validated COMPOUND plan and diagnostic name.");
                                    }

                                    COMPOUND4res decodedReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        reply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedReply.status != nfsstat4.NFS4_OK
                                        || decodedReply.resarray is null
                                        || decodedReply.resarray.Length != 2)
                                    {
                                        throw new InvalidOperationException("Expected raw NFSv4.0 execution to return a successful COMPOUND reply payload.");
                                    }

                                    byte[] acceptedPayload = reply.ReadAcceptedSuccessProcedurePayload();
                                    if (acceptedPayload.Length < 1)
                                    {
                                        throw new InvalidOperationException("Expected raw NFSv4.0 execution to preserve the accepted-success procedure payload bytes.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "RawCompoundExecutionNegative",
                        displayName: "Raw NFSv4.0 COMPOUND execution surfaces negative protocol results without losing the reply payload",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 1,
                                async client =>
                                {
                                    OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-negative",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutFileHandleOperation(rootHandle.ToArray()),
                                                CreateLookupOperation("missing.txt"),
                                                new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        reply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedReply.status != nfsstat4.NFS4ERR_NOENT
                                        || decodedReply.resarray is null
                                        || decodedReply.resarray.Length != 2
                                        || decodedReply.resarray[1].oplookup?.status != nfsstat4.NFS4ERR_NOENT)
                                    {
                                        throw new InvalidOperationException("Expected raw NFSv4.0 execution to preserve the short-circuited negative LOOKUP result.");
                                    }

                                    if (reply.ReadAcceptedSuccessProcedurePayload().Length < 1)
                                    {
                                        throw new InvalidOperationException("Expected raw NFSv4.0 negative execution to still preserve the accepted-success COMPOUND payload.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "RawCompoundAdvancedOpsPositive",
                        displayName: "Raw NFSv4.0 COMPOUND execution preserves explicit public-root and verify operation fidelity",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 2,
                                async client =>
                                {
                                    OpenNfsCompoundReply verifyReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-positive-verify",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutPublicFileHandleOperation(),
                                                CreateVerifyOperation(nfs_ftype4.NF4DIR),
                                                new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedVerifyReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        verifyReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    byte[]? publicHandleBytes = decodedVerifyReply.resarray?[2].opgetfh?.resok4?.@object?.Value;
                                    if (decodedVerifyReply.status != nfsstat4.NFS4_OK
                                        || decodedVerifyReply.resarray is null
                                        || decodedVerifyReply.resarray.Length != 3
                                        || decodedVerifyReply.resarray[0].opputpubfh?.status != nfsstat4.NFS4_OK
                                        || decodedVerifyReply.resarray[1].opverify?.status != nfsstat4.NFS4_OK
                                        || decodedVerifyReply.resarray[2].opgetfh?.status != nfsstat4.NFS4_OK
                                        || publicHandleBytes is null
                                        || !publicHandleBytes.AsSpan().SequenceEqual(rootHandle.ToArray()))
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve PUTPUBFH, VERIFY, and GETFH fidelity over the public transport path.");
                                    }

                                    OpenNfsCompoundReply nverifyReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-positive-nverify",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutPublicFileHandleOperation(),
                                                CreateNotVerifyOperation(nfs_ftype4.NF4REG),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedNverifyReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        nverifyReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedNverifyReply.status != nfsstat4.NFS4_OK
                                        || decodedNverifyReply.resarray is null
                                        || decodedNverifyReply.resarray.Length != 2
                                        || decodedNverifyReply.resarray[1].opnverify?.status != nfsstat4.NFS4_OK)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve successful NVERIFY execution over the public transport path.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "RawCompoundAdvancedOpsNegative",
                        displayName: "Raw NFSv4.0 COMPOUND execution preserves negative verify and capability-gated operation results",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 5,
                                async client =>
                                {
                                    OpenNfsCompoundReply verifyReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-negative-verify",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutPublicFileHandleOperation(),
                                                CreateVerifyOperation(nfs_ftype4.NF4REG),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedVerifyReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        verifyReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedVerifyReply.status != nfsstat4.NFS4ERR_NOT_SAME
                                        || decodedVerifyReply.resarray is null
                                        || decodedVerifyReply.resarray.Length != 2
                                        || decodedVerifyReply.resarray[1].opverify?.status != nfsstat4.NFS4ERR_NOT_SAME)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve VERIFY mismatch results.");
                                    }

                                    OpenNfsCompoundReply nverifyReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-negative-nverify",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutPublicFileHandleOperation(),
                                                CreateNotVerifyOperation(nfs_ftype4.NF4DIR),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedNverifyReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        nverifyReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedNverifyReply.status != nfsstat4.NFS4ERR_SAME
                                        || decodedNverifyReply.resarray is null
                                        || decodedNverifyReply.resarray.Length != 2
                                        || decodedNverifyReply.resarray[1].opnverify?.status != nfsstat4.NFS4ERR_SAME)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve NVERIFY same-attribute results.");
                                    }

                                    OpenNfsCompoundReply openAttributeReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-negative-openattr",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreatePutPublicFileHandleOperation(),
                                                CreateOpenAttributeOperation(createdir: false),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedOpenAttributeReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        openAttributeReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedOpenAttributeReply.status != nfsstat4.NFS4ERR_NOTSUPP
                                        || decodedOpenAttributeReply.resarray is null
                                        || decodedOpenAttributeReply.resarray.Length != 2
                                        || decodedOpenAttributeReply.resarray[1].opopenattr?.status != nfsstat4.NFS4ERR_NOTSUPP)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve OPENATTR capability-gated NOTSUPP results.");
                                    }

                                    OpenNfsCompoundReply delegationPurgeReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-negative-delegpurge",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreateDelegationPurgeOperation(0UL),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedDelegationPurgeReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        delegationPurgeReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedDelegationPurgeReply.status != nfsstat4.NFS4ERR_NOTSUPP
                                        || decodedDelegationPurgeReply.resarray is null
                                        || decodedDelegationPurgeReply.resarray.Length != 1
                                        || decodedDelegationPurgeReply.resarray[0].opdelegpurge?.status != nfsstat4.NFS4ERR_NOTSUPP)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve DELEGPURGE capability-gated NOTSUPP results.");
                                    }

                                    OpenNfsCompoundReply releaseLockOwnerReply = await client.ExecuteCompoundAsync(
                                        new OpenNfsCompoundRequest(
                                            OpenNfsProtocolVersion.Nfs40,
                                            "raw-advanced-negative-release-lockowner",
                                            new OpenNfsCompoundOperation[]
                                            {
                                                CreateReleaseLockOwnerOperation(0UL, new byte[] { 0x01 }),
                                            }),
                                        OpenNfsOperationIdempotency.Idempotent,
                                        cancellationToken).ConfigureAwait(false);

                                    COMPOUND4res decodedReleaseLockOwnerReply = OpenNfsV40ReplyDecoder.ReadCompoundResult(
                                        releaseLockOwnerReply.EncodedReply,
                                        "Raw NFSv4.0 COMPOUND");
                                    if (decodedReleaseLockOwnerReply.status != nfsstat4.NFS4ERR_NOTSUPP
                                        || decodedReleaseLockOwnerReply.resarray is null
                                        || decodedReleaseLockOwnerReply.resarray.Length != 1
                                        || decodedReleaseLockOwnerReply.resarray[0].oprelease_lockowner?.status != nfsstat4.NFS4ERR_NOTSUPP)
                                    {
                                        throw new InvalidOperationException("Expected the raw NFSv4.0 client surface to preserve RELEASE_LOCKOWNER capability-gated NOTSUPP results.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}
