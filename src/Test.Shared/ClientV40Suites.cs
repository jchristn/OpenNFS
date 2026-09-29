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

    /// <summary>
    /// Touchstone suites covering the first executable NFSv4.0 public client surface.
    /// </summary>
    public static class ClientV40Suites
    {
        /// <summary>
        /// Creates the shared NFSv4.0 client suite catalog.
        /// </summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClientV40Suites",
                displayName: "Client NFSv4.0 Surface",
                cases: new List<TestCaseDescriptor>
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

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedApisPositive",
                        displayName: "Grouped NFSv4.0 file and directory APIs prepare, execute, and decode successful read-only flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle shortcutHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\shortcut"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 6,
                                async client =>
                                {
                                    OpenNfsCompoundPlan lookupPlan = await client.Directories.PrepareLookupV40Async(
                                        docsHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan readPlan = await client.Files.PrepareReadV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        5U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (lookupPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lookupPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || lookupPlan.Operations.Count != 4
                                        || readPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || readPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || readPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 planning to stay on TCP and emit the expected COMPOUND shape.");
                                    }

                                    OpenNfsV40GetAttributesResult getattrResult =
                                        await client.Files.GetAttributesV40Async(noteHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40AccessResult accessResult =
                                        await client.Files.AccessV40Async(noteHandle.ToArray(), OpenNfsV40AccessMask.Read, cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult lookupResult =
                                        await client.Directories.LookupV40Async(docsHandle.ToArray(), "notes.txt", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadResult readResult =
                                        await client.Files.ReadV40Async(noteHandle.ToArray(), 0UL, 5U, cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadLinkResult readLinkResult =
                                        await client.Files.ReadLinkV40Async(shortcutHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadDirectoryResult readDirectoryResult =
                                        await client.Directories.ReadDirectoryV40Async(rootHandle.ToArray(), 0UL, new byte[8], 4096U, cancellationToken).ConfigureAwait(false);

                                    if (!getattrResult.IsSuccess
                                        || getattrResult.Attributes?.FileType != OpenNfsV40FileType.RegularFile
                                        || getattrResult.Attributes.SizeBytes != 8UL
                                        || !accessResult.IsSuccess
                                        || accessResult.GrantedAccess != OpenNfsV40AccessMask.Read
                                        || !lookupResult.IsSuccess
                                        || !lookupResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                        || lookupResult.ObjectAttributes?.FileType != OpenNfsV40FileType.RegularFile
                                        || !readResult.IsSuccess
                                        || Encoding.UTF8.GetString(readResult.Data.Span) != "hello"
                                        || !readLinkResult.IsSuccess
                                        || !string.Equals(readLinkResult.TargetPath, "notes.txt", StringComparison.Ordinal)
                                        || !readDirectoryResult.IsSuccess
                                        || readDirectoryResult.Entries.Count != 1
                                        || !string.Equals(readDirectoryResult.Entries[0].Name, "docs", StringComparison.Ordinal))
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 APIs to execute and decode the current read-only protocol surface.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedApisNegative",
                        displayName: "Grouped NFSv4.0 file and directory APIs surface negative protocol results cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 4,
                                async client =>
                                {
                                    OpenNfsV40LookupResult missingLookup =
                                        await client.Directories.LookupV40Async(docsHandle.ToArray(), "missing.txt", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadResult directoryRead =
                                        await client.Files.ReadV40Async(rootHandle.ToArray(), 0UL, 16U, cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadLinkResult invalidReadLink =
                                        await client.Files.ReadLinkV40Async(noteHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadDirectoryResult badCookieRead =
                                        await client.Directories.ReadDirectoryV40Async(rootHandle.ToArray(), 1UL, new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, 4096U, cancellationToken).ConfigureAwait(false);

                                    if (missingLookup.Status != OpenNfsV40Status.NoEnt
                                        || directoryRead.Status != OpenNfsV40Status.IsDirectory
                                        || invalidReadLink.Status != OpenNfsV40Status.Invalid
                                        || badCookieRead.Status != OpenNfsV40Status.BadCookie)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 APIs to preserve negative protocol results for missing LOOKUP, invalid READ, invalid READLINK, and bad-cookie READDIR.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedSecurityAndIdentityApisPositive",
                        displayName: "Grouped NFSv4.0 SECINFO and identity-attribute APIs decode successful security discovery and owner mapping",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer(
                                new TestNfsIdMapper(
                                    owner: "owner@example.test",
                                    ownerGroup: "group@example.test"));
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 2,
                                async client =>
                                {
                                    OpenNfsCompoundPlan securityPlan = await client.Directories.PrepareGetSecurityInfoV40Async(
                                        docsHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan attributesPlan = await client.Files.PrepareGetAttributesV40Async(
                                        noteHandle.ToArray(),
                                        new[]
                                        {
                                            OpenNfsV40AttributeKind.Type,
                                            OpenNfsV40AttributeKind.Owner,
                                            OpenNfsV40AttributeKind.OwnerGroup,
                                        },
                                        cancellationToken).ConfigureAwait(false);

                                    if (securityPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || securityPlan.Operations.Count != 2
                                        || attributesPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || attributesPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and selected-GETATTR planning to emit the expected COMPOUND shape.");
                                    }

                                    OpenNfsV40SecurityInfoResult securityInfoResult =
                                        await client.Directories.GetSecurityInfoV40Async(
                                            docsHandle.ToArray(),
                                            "notes.txt",
                                            cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetAttributesResult attributesResult =
                                        await client.Files.GetAttributesV40Async(
                                            noteHandle.ToArray(),
                                            new[]
                                            {
                                                OpenNfsV40AttributeKind.Type,
                                                OpenNfsV40AttributeKind.Owner,
                                                OpenNfsV40AttributeKind.OwnerGroup,
                                            },
                                            cancellationToken).ConfigureAwait(false);

                                    if (!securityInfoResult.IsSuccess
                                        || securityInfoResult.SecurityFlavors.Count != 2
                                        || securityInfoResult.SecurityFlavors[0].Flavor != OpenNfsRpcAuthenticationFlavor.AuthNone
                                        || securityInfoResult.SecurityFlavors[1].Flavor != OpenNfsRpcAuthenticationFlavor.AuthSys
                                        || !attributesResult.IsSuccess
                                        || attributesResult.Attributes?.FileType != OpenNfsV40FileType.RegularFile
                                        || !string.Equals(attributesResult.Attributes.Owner, "owner@example.test", StringComparison.Ordinal)
                                        || !string.Equals(attributesResult.Attributes.OwnerGroup, "group@example.test", StringComparison.Ordinal))
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and identity attributes to surface AUTH_NONE, AUTH_SYS, owner, and owner-group data.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedSecurityAndIdentityApisNegative",
                        displayName: "Grouped NFSv4.0 SECINFO and identity-attribute APIs surface negative discovery and unsupported-attribute results cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 2,
                                async client =>
                                {
                                    OpenNfsV40SecurityInfoResult missingEntrySecurityInfo =
                                        await client.Directories.GetSecurityInfoV40Async(
                                            docsHandle.ToArray(),
                                            "missing.txt",
                                            cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetAttributesResult unsupportedAttributes =
                                        await client.Files.GetAttributesV40Async(
                                            noteHandle.ToArray(),
                                            new[]
                                            {
                                                OpenNfsV40AttributeKind.Owner,
                                                OpenNfsV40AttributeKind.OwnerGroup,
                                            },
                                            cancellationToken).ConfigureAwait(false);

                                    if (missingEntrySecurityInfo.Status != OpenNfsV40Status.NoEnt
                                        || unsupportedAttributes.Status != OpenNfsV40Status.AttributeNotSupported)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and identity attribute requests to preserve NOENT and ATTRNOTSUPP failures.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedAclApisPositive",
                        displayName: "Grouped NFSv4.0 ACL APIs round-trip ACL support and ACL replacement successfully",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsAcls acls = new TestNfsAcls(
                                initialEntries: new Dictionary<string, IReadOnlyList<NfsAclEntry>>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = new[]
                                    {
                                        new NfsAclEntry(
                                            NfsAclEntryType.Allow,
                                            NfsAclEntryFlags.None,
                                            NfsAclPermissionMask.ReadData | NfsAclPermissionMask.ReadAcl,
                                            "EVERYONE@"),
                                    },
                                });

                            OpenNfsServer server = CreateServer(acls: acls);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 3,
                                async client =>
                                {
                                    OpenNfsV40GetAclResult initialAclResult = await client.Files.GetAclV40Async(
                                        noteHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40AclEntry[] updatedAcl = new[]
                                    {
                                        new OpenNfsV40AclEntry(
                                            OpenNfsV40AclEntryType.Allow,
                                            OpenNfsV40AclEntryFlags.None,
                                            OpenNfsV40AclPermissionMask.ReadData
                                                | OpenNfsV40AclPermissionMask.WriteData
                                                | OpenNfsV40AclPermissionMask.ReadAcl,
                                            "interop-user@example.test"),
                                        new OpenNfsV40AclEntry(
                                            OpenNfsV40AclEntryType.Deny,
                                            OpenNfsV40AclEntryFlags.None,
                                            OpenNfsV40AclPermissionMask.Delete,
                                            "EVERYONE@"),
                                    };

                                    OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                                        noteHandle.ToArray(),
                                        updatedAcl,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetAclResult rereadAclResult = await client.Files.GetAclV40Async(
                                        noteHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);

                                    if (!initialAclResult.IsSuccess
                                        || initialAclResult.SupportedAcls != (OpenNfsV40AclSupport.AllowAcl | OpenNfsV40AclSupport.DenyAcl)
                                        || initialAclResult.Entries.Count != 1
                                        || !string.Equals(initialAclResult.Entries[0].Who, "EVERYONE@", StringComparison.Ordinal)
                                        || !setAclResult.IsSuccess
                                        || setAclResult.SetAttributeMaskWords.Count < 1
                                        || (setAclResult.SetAttributeMaskWords[0] & (1U << (int)OpenNfsV40AttributeKind.Acl)) == 0U
                                        || !rereadAclResult.IsSuccess
                                        || rereadAclResult.Entries.Count != 2
                                        || !string.Equals(rereadAclResult.Entries[0].Who, "interop-user@example.test", StringComparison.Ordinal)
                                        || rereadAclResult.Entries[0].Permissions != (
                                            OpenNfsV40AclPermissionMask.ReadData
                                            | OpenNfsV40AclPermissionMask.WriteData
                                            | OpenNfsV40AclPermissionMask.ReadAcl)
                                        || rereadAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 ACL helpers to round-trip ACL support flags and replacement ACL entries.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedAclApisNegative",
                        displayName: "Grouped NFSv4.0 ACL APIs surface unsupported-attribute results cleanly when ACL capability is absent",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 2,
                                async client =>
                                {
                                    OpenNfsV40GetAclResult getAclResult = await client.Files.GetAclV40Async(
                                        noteHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                                        noteHandle.ToArray(),
                                        new[]
                                        {
                                            new OpenNfsV40AclEntry(
                                                OpenNfsV40AclEntryType.Allow,
                                                OpenNfsV40AclEntryFlags.None,
                                                OpenNfsV40AclPermissionMask.ReadData,
                                                "EVERYONE@"),
                                        },
                                        cancellationToken).ConfigureAwait(false);

                                    if (getAclResult.Status != OpenNfsV40Status.AttributeNotSupported
                                        || setAclResult.Status != OpenNfsV40Status.AttributeNotSupported)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 ACL helpers to preserve ATTRNOTSUPP when the host does not expose ACL capability.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedMutationApisPositive",
                        displayName: "Grouped NFSv4.0 directory mutation APIs prepare, execute, and decode successful namespace changes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 6,
                                async client =>
                                {
                                    OpenNfsCompoundPlan lookuppPlan = await client.Directories.PrepareLookupParentV40Async(
                                        noteHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan createPlan = await client.Directories.PrepareCreateDirectoryV40Async(
                                        docsHandle.ToArray(),
                                        "newdir",
                                        cancellationToken).ConfigureAwait(false);

                                    if (lookuppPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lookuppPlan.Operations.Count != 4
                                        || createPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || createPlan.Operations.Count != 4)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 mutation planning to preserve the expected COMPOUND shapes.");
                                    }

                                    OpenNfsV40LookupResult parentLookup =
                                        await client.Directories.LookupParentV40Async(docsHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CreateResult createDirectory =
                                        await client.Directories.CreateDirectoryV40Async(docsHandle.ToArray(), "newdir", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CreateResult createSymbolicLink =
                                        await client.Directories.CreateSymbolicLinkV40Async(docsHandle.ToArray(), "generated-link", "notes.txt", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LinkResult createHardLink =
                                        await client.Directories.CreateHardLinkV40Async(noteHandle.ToArray(), rootHandle.ToArray(), "notes-link.txt", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40RenameResult renameResult =
                                        await client.Directories.RenameV40Async(docsHandle.ToArray(), "newdir", rootHandle.ToArray(), "renamed-dir", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DirectoryMutationResult removeResult =
                                        await client.Directories.RemoveEntryV40Async(rootHandle.ToArray(), "renamed-dir", cancellationToken).ConfigureAwait(false);

                                    if (!parentLookup.IsSuccess
                                        || !parentLookup.ObjectFileHandle.Span.SequenceEqual(rootHandle.ToArray())
                                        || parentLookup.ObjectAttributes?.FileType != OpenNfsV40FileType.Directory
                                        || !createDirectory.IsSuccess
                                        || createDirectory.ObjectAttributes?.FileType != OpenNfsV40FileType.Directory
                                        || !createSymbolicLink.IsSuccess
                                        || createSymbolicLink.ObjectAttributes?.FileType != OpenNfsV40FileType.SymbolicLink
                                        || !createHardLink.IsSuccess
                                        || createHardLink.DirectoryChangeInfo is null
                                        || !renameResult.IsSuccess
                                        || renameResult.SourceDirectoryChangeInfo is null
                                        || renameResult.TargetDirectoryChangeInfo is null
                                        || !removeResult.IsSuccess
                                        || removeResult.DirectoryChangeInfo is null)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 mutation APIs to execute and decode successful LOOKUPP, CREATE, LINK, RENAME, and REMOVE flows.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedMutationApisNegative",
                        displayName: "Grouped NFSv4.0 directory mutation APIs surface negative namespace results cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateCrossExportServer();
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle otherRootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/other", @"C:\other"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 5,
                                async client =>
                                {
                                    OpenNfsV40LookupResult rootParent =
                                        await client.Directories.LookupParentV40Async(rootHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CreateResult existingDirectory =
                                        await client.Directories.CreateDirectoryV40Async(rootHandle.ToArray(), "docs", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LinkResult invalidHardLink =
                                        await client.Directories.CreateHardLinkV40Async(docsHandle.ToArray(), rootHandle.ToArray(), "dir-link", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40RenameResult crossExportRename =
                                        await client.Directories.RenameV40Async(docsHandle.ToArray(), "notes.txt", otherRootHandle.ToArray(), "moved.txt", cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DirectoryMutationResult missingRemove =
                                        await client.Directories.RemoveEntryV40Async(rootHandle.ToArray(), "missing", cancellationToken).ConfigureAwait(false);

                                    if (rootParent.Status != OpenNfsV40Status.NoEnt
                                        || existingDirectory.Status != OpenNfsV40Status.Exists
                                        || invalidHardLink.Status != OpenNfsV40Status.IsDirectory
                                        || crossExportRename.Status != OpenNfsV40Status.CrossDevice
                                        || missingRemove.Status != OpenNfsV40Status.NoEnt)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 mutation APIs to preserve negative protocol results for LOOKUPP, CREATE, LINK, RENAME, and REMOVE.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedStatefulApisPositive",
                        displayName: "Grouped NFSv4.0 session and open-state APIs prepare, execute, and decode successful stateful flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 7,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

                                    OpenNfsCompoundPlan setClientIdPlan = await client.Sessions.PrepareSetClientIdV40Async(
                                        "client-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan openPlan = await client.Files.PrepareOpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        clientId: 1UL,
                                        openOwner: "owner-a",
                                        entryName: "notes.txt",
                                        shareAccess: OpenNfsV40ShareAccess.Read,
                                        shareDeny: OpenNfsV40ShareDeny.Write,
                                        sequenceId: 1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (setClientIdPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || setClientIdPlan.Operations.Count != 1
                                        || openPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || openPlan.Operations.Count != 4)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful planning to preserve the expected COMPOUND shapes.");
                                    }

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.Write,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult renewResult = await client.Sessions.RenewV40Async(
                                        setClientIdResult.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult downgradeResult = await client.Files.DowngradeOpenV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        downgradeResult.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || setClientIdResult.ConfirmationVerifier.Length != 8
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || openResult.StateId is null
                                        || !openResult.RequiresConfirmation
                                        || !openResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                        || openResult.ObjectAttributes?.FileType != OpenNfsV40FileType.RegularFile
                                        || !openConfirmResult.IsSuccess
                                        || openConfirmResult.StateId?.SequenceId != 2U
                                        || !renewResult.IsSuccess
                                        || !downgradeResult.IsSuccess
                                        || downgradeResult.StateId?.SequenceId != 3U
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 4U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful APIs to execute and decode successful SETCLIENTID, OPEN, OPEN_CONFIRM, RENEW, OPEN_DOWNGRADE, and CLOSE flows.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedStatefulApisNegative",
                        displayName: "Grouped NFSv4.0 session and open-state APIs surface negative clientid and state results cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 10,
                                async client =>
                                {
                                    byte[] verifierA = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 };
                                    OpenNfsV40SetClientIdResult setClientIdA = await client.Sessions.SetClientIdV40Async(
                                        "client-a",
                                        verifierA,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult staleOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult wrongConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        new byte[8],
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmA = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        setClientIdA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult firstOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.Write,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult confirmOpenA = await client.Files.ConfirmOpenV40Async(
                                        firstOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    byte[] verifierB = new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 };
                                    OpenNfsV40SetClientIdResult setClientIdB = await client.Sessions.SetClientIdV40Async(
                                        "client-b",
                                        verifierB,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmB = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdB.ClientId,
                                        setClientIdB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult shareDeniedOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Write,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult badStateClose = await client.Files.CloseV40Async(
                                        new OpenNfsV40StateId(confirmOpenA.StateId!.SequenceId, new byte[12]),
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdA.IsSuccess
                                        || staleOpen.Status != OpenNfsV40Status.StaleClientId
                                        || wrongConfirm.Status != OpenNfsV40Status.StaleClientId
                                        || !confirmA.IsSuccess
                                        || !firstOpen.IsSuccess
                                        || !confirmOpenA.IsSuccess
                                        || !setClientIdB.IsSuccess
                                        || !confirmB.IsSuccess
                                        || shareDeniedOpen.Status != OpenNfsV40Status.ShareDenied
                                        || badStateClose.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful APIs to preserve negative protocol results for stale clientids, share denial, and bad stateids.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedWriteApisPositive",
                        displayName: "Grouped NFSv4.0 write APIs prepare, execute, and decode successful WRITE, COMMIT, and read-back flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 8,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 24, 25, 26, 27, 28, 29, 30, 31 };
                                    byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-v40");

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-write-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-write-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan writePlan = await client.Files.PrepareWriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        updatedBytes,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan commitPlan = await client.Files.PrepareCommitV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        (uint)updatedBytes.Length,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        updatedBytes,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        (uint)updatedBytes.Length,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadResult readResult = await client.Files.ReadV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        64U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || writePlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || writePlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || writePlan.Operations.Count != 2
                                        || commitPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || commitPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || commitPlan.Operations.Count != 2
                                        || !writeResult.IsSuccess
                                        || writeResult.Count != (uint)updatedBytes.Length
                                        || writeResult.CommittedStability != OpenNfsWriteStability.FileSync
                                        || writeResult.Verifier.Length != 8
                                        || !commitResult.IsSuccess
                                        || commitResult.Verifier.Length != 8
                                        || !commitResult.Verifier.Span.SequenceEqual(writeResult.Verifier.Span)
                                        || !readResult.IsSuccess
                                        || Encoding.UTF8.GetString(readResult.Data.Span) != "updated-v40"
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 WRITE and COMMIT APIs to preserve stable write acknowledgements, verifier reuse, read-back content, and close sequencing.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedWriteApisNegative",
                        displayName: "Grouped NFSv4.0 write APIs surface OPENMODE, BAD_STATEID, and directory COMMIT failures cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 8,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 32, 33, 34, 35, 36, 37, 38, 39 };

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-write-negative-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-write-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40WriteResult openModeWriteResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        Encoding.UTF8.GetBytes("x"),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40WriteResult badStateWriteResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        new OpenNfsV40StateId(openConfirmResult.StateId!.SequenceId, new byte[12]),
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        Encoding.UTF8.GetBytes("y"),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CommitResult directoryCommitResult = await client.Files.CommitV40Async(
                                        docsHandle.ToArray(),
                                        0UL,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || openModeWriteResult.Status != OpenNfsV40Status.OpenMode
                                        || badStateWriteResult.Status != OpenNfsV40Status.BadStateId
                                        || directoryCommitResult.Status != OpenNfsV40Status.IsDirectory
                                        || !closeResult.IsSuccess)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 WRITE and COMMIT failures to preserve OPENMODE, BAD_STATEID, and directory-commit behavior.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLockApisPositive",
                        displayName: "Grouped NFSv4.0 lock APIs prepare, execute, and decode successful LOCK, relock, LOCKU, and CLOSE flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 8,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 };

                                    OpenNfsCompoundPlan setClientIdPlan = await client.Sessions.PrepareSetClientIdV40Async(
                                        "client-lock-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-lock-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan lockFromOpenPlan = await client.Locks.PrepareLockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult firstLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult relockResult = await client.Locks.LockV40Async(
                                        noteHandle.ToArray(),
                                        firstLockResult.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan unlockPlan = await client.Locks.PrepareUnlockV40Async(
                                        noteHandle.ToArray(),
                                        relockResult.StateId!,
                                        3U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        relockResult.StateId!,
                                        3U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (setClientIdPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || setClientIdPlan.Operations.Count != 1)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 SETCLIENTID planning to preserve a single-operation NFSv4.0 COMPOUND.");
                                    }

                                    if (!setClientIdResult.IsSuccess || !confirmClientIdResult.IsSuccess)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup to confirm the clientid successfully.");
                                    }

                                    if (!openResult.IsSuccess
                                        || !openResult.RequiresConfirmation
                                        || !openResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray()))
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup OPEN to return a confirm-required stateid and switch the current filehandle to the opened file.");
                                    }

                                    if (!openConfirmResult.IsSuccess || openConfirmResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup OPEN_CONFIRM to advance the open stateid sequence to 2.");
                                    }

                                    if (lockFromOpenPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lockFromOpenPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || lockFromOpenPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCK planning to preserve a two-operation TCP-only COMPOUND.");
                                    }

                                    if (!firstLockResult.IsSuccess || firstLockResult.StateId?.SequenceId != 1U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCK-from-open execution to return the first lock stateid sequence.");
                                    }

                                    if (!relockResult.IsSuccess || relockResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 relock execution to advance the lock stateid sequence to 2.");
                                    }

                                    if (unlockPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || unlockPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || unlockPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCKU planning to preserve a two-operation TCP-only COMPOUND.");
                                    }

                                    if (!unlockResult.IsSuccess || unlockResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCKU execution to advance the lock stateid sequence to 3.");
                                    }

                                    if (!closeResult.IsSuccess || closeResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 CLOSE execution to advance the open stateid sequence to 3 after the lock is released.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLockApisNegative",
                        displayName: "Grouped NFSv4.0 lock APIs surface denied lock, lock-held close, and bad-state unlock outcomes cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 13,
                                async client =>
                                {
                                    byte[] verifierA = new byte[] { 41, 42, 43, 44, 45, 46, 47, 48 };
                                    OpenNfsV40SetClientIdResult setClientIdA = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-negative-a",
                                        verifierA,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdA = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        setClientIdA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResultA = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-lock-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResultA = await client.Files.ConfirmOpenV40Async(
                                        openResultA.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult firstLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResultA.StateId!,
                                        3U,
                                        setClientIdA.ClientId,
                                        "lock-owner-negative-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    byte[] verifierB = new byte[] { 51, 52, 53, 54, 55, 56, 57, 58 };
                                    OpenNfsV40SetClientIdResult setClientIdB = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-negative-b",
                                        verifierB,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdB = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdB.ClientId,
                                        setClientIdB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResultB = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "owner-lock-negative-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResultB = await client.Files.ConfirmOpenV40Async(
                                        openResultB.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan lockTestPlan = await client.Locks.PrepareTestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        OpenNfsV40LockType.Read,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult lockTestResult = await client.Locks.TestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        OpenNfsV40LockType.Read,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult lockDeniedResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResultB.StateId!,
                                        3U,
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeWhileLockedResult = await client.Files.CloseV40Async(
                                        openConfirmResultA.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult badUnlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        new OpenNfsV40StateId(firstLockResult.StateId!.SequenceId, new byte[12]),
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdA.IsSuccess
                                        || !confirmClientIdA.IsSuccess
                                        || !openResultA.IsSuccess
                                        || !openConfirmResultA.IsSuccess
                                        || !firstLockResult.IsSuccess
                                        || !setClientIdB.IsSuccess
                                        || !confirmClientIdB.IsSuccess
                                        || !openResultB.IsSuccess
                                        || !openConfirmResultB.IsSuccess
                                        || lockTestPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lockTestPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || lockTestPlan.Operations.Count != 2
                                        || lockTestResult.Status != OpenNfsV40Status.Denied
                                        || lockTestResult.Conflict?.ClientId != setClientIdA.ClientId
                                        || Encoding.UTF8.GetString(lockTestResult.Conflict?.Owner.ToArray() ?? Array.Empty<byte>()) != "lock-owner-negative-a"
                                        || lockDeniedResult.Status != OpenNfsV40Status.Denied
                                        || lockDeniedResult.Conflict?.ClientId != setClientIdA.ClientId
                                        || closeWhileLockedResult.Status != OpenNfsV40Status.LocksHeld
                                        || badUnlockResult.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock APIs to preserve denied conflict, lock-held close, and bad-state unlock results.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedRecoveryApisPositive",
                        displayName: "Grouped NFSv4.0 state and lock APIs reclaim open and lock state successfully during grace",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero));
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 9,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 61, 62, 63, 64, 65, 66, 67, 68 };
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-recovery-a",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    service.SimulateRecovery();
                                    if (!service.IsGracePeriodActive())
                                    {
                                        throw new InvalidOperationException("Expected simulated recovery to enter grace before grouped reclaim calls run.");
                                    }

                                    OpenNfsCompoundPlan reclaimPlan = await client.Files.PrepareReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult reclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult reclaimLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        reclaimOpenResult.StateId!,
                                        2U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: true,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        reclaimLockResult.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        reclaimOpenResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || !initialLockResult.IsSuccess
                                        || reclaimPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || reclaimPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || reclaimPlan.Operations.Count != 4
                                        || !reclaimOpenResult.IsSuccess
                                        || reclaimOpenResult.RequiresConfirmation
                                        || reclaimOpenResult.StateId?.SequenceId != 1U
                                        || !reclaimOpenResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                        || !reclaimLockResult.IsSuccess
                                        || reclaimLockResult.StateId?.SequenceId != 1U
                                        || !unlockResult.IsSuccess
                                        || unlockResult.StateId?.SequenceId != 2U
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 recovery APIs to reclaim open and lock state during grace without requiring OPEN_CONFIRM.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedRecoveryApisNegative",
                        displayName: "Grouped NFSv4.0 state and lock APIs surface grace and reclaim failures cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 15, 0, 0, TimeSpan.Zero));
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 9,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 71, 72, 73, 74, 75, 76, 77, 78 };
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-recovery-negative-a",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-negative-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    service.SimulateRecovery();

                                    OpenNfsV40OpenResult graceOpenResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult graceLockTestResult = await client.Locks.TestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-negative-a",
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult badReclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "wrong-owner",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));
                                    OpenNfsV40OpenResult lateReclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || !initialLockResult.IsSuccess
                                        || graceOpenResult.Status != OpenNfsV40Status.Grace
                                        || graceLockTestResult.Status != OpenNfsV40Status.Grace
                                        || badReclaimOpenResult.Status != OpenNfsV40Status.ReclaimBad
                                        || lateReclaimOpenResult.Status != OpenNfsV40Status.NoGrace
                                        || service.IsGracePeriodActive())
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 recovery APIs to preserve grace, reclaim-bad, and no-grace failures around simulated recovery.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLeaseExpiryRecoveryPositive",
                        displayName: "Grouped NFSv4.0 APIs recover from lease expiry by re-registering and reopening fresh state",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 13,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 81, 82, 83, 84, 85, 86, 87, 88 };
                                    OpenNfsV40SetClientIdResult initialSetClientId = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-recovery",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult initialConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        initialSetClientId.ClientId,
                                        initialSetClientId.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult initialOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        initialSetClientId.ClientId,
                                        "owner-lease-recovery",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult initialConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        initialOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        initialConfirmOpen.StateId!,
                                        3U,
                                        initialSetClientId.ClientId,
                                        "lock-owner-lease-recovery",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));

                                    OpenNfsV40SetClientIdResult recoveredSetClientId = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-recovery",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult recoveredConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        recoveredSetClientId.ClientId,
                                        recoveredSetClientId.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult recoveredOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        recoveredSetClientId.ClientId,
                                        "owner-lease-recovery",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult recoveredConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        recoveredOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult recoveredRenew = await client.Sessions.RenewV40Async(
                                        recoveredSetClientId.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult recoveredLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        recoveredConfirmOpen.StateId!,
                                        3U,
                                        recoveredSetClientId.ClientId,
                                        "lock-owner-lease-recovery",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult recoveredUnlock = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        recoveredLock.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult recoveredClose = await client.Files.CloseV40Async(
                                        recoveredConfirmOpen.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!initialSetClientId.IsSuccess
                                        || !initialConfirm.IsSuccess
                                        || !initialOpen.IsSuccess
                                        || !initialConfirmOpen.IsSuccess
                                        || !initialLock.IsSuccess
                                        || !recoveredSetClientId.IsSuccess
                                        || recoveredSetClientId.ClientId == initialSetClientId.ClientId
                                        || !recoveredConfirm.IsSuccess
                                        || !recoveredOpen.IsSuccess
                                        || !recoveredConfirmOpen.IsSuccess
                                        || !recoveredRenew.IsSuccess
                                        || !recoveredLock.IsSuccess
                                        || !recoveredUnlock.IsSuccess
                                        || !recoveredClose.IsSuccess
                                        || recoveredClose.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 clients to recover from lease expiry by re-registering and driving a fresh open, renew, lock, unlock, and close flow.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLeaseExpiryRecoveryNegative",
                        displayName: "Grouped NFSv4.0 APIs surface BAD_STATEID and STALE_CLIENTID results before lease-expiry recovery",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 13, 0, 0, TimeSpan.Zero));
                            (OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle) =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 10,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult stateClientSet = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-negative-state",
                                        new byte[] { 91, 92, 93, 94, 95, 96, 97, 98 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult stateClientConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        stateClientSet.ClientId,
                                        stateClientSet.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult stateClientOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        stateClientSet.ClientId,
                                        "owner-lease-negative-state",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult stateClientConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        stateClientOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult stateClientLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        stateClientConfirmOpen.StateId!,
                                        3U,
                                        stateClientSet.ClientId,
                                        "lock-owner-lease-negative-state",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40SetClientIdResult renewClientSet = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-negative-renew",
                                        new byte[] { 101, 102, 103, 104, 105, 106, 107, 108 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult renewClientConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        renewClientSet.ClientId,
                                        renewClientSet.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));

                                    OpenNfsV40LockResult expiredUnlock = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        stateClientLock.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult expiredRenew = await client.Sessions.RenewV40Async(
                                        renewClientSet.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult staleOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        stateClientSet.ClientId,
                                        "owner-lease-negative-state",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!stateClientSet.IsSuccess
                                        || !stateClientConfirm.IsSuccess
                                        || !stateClientOpen.IsSuccess
                                        || !stateClientConfirmOpen.IsSuccess
                                        || !stateClientLock.IsSuccess
                                        || !renewClientSet.IsSuccess
                                        || !renewClientConfirm.IsSuccess
                                        || expiredUnlock.Status != OpenNfsV40Status.BadStateId
                                        || expiredRenew.Status != OpenNfsV40Status.StaleClientId
                                        || staleOpen.Status != OpenNfsV40Status.StaleClientId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 clients to surface BAD_STATEID and STALE_CLIENTID after lease-expired state has been purged.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedDelegationApisPositive",
                        displayName: "Grouped NFSv4.0 delegation APIs grant, recall, and return read delegations",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsDelegations delegations = new TestNfsDelegations(
                                new Dictionary<string, OpenNFS.Server.Delegations.NfsDelegationKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = OpenNFS.Server.Delegations.NfsDelegationKind.Read,
                                });
                            OpenNfsServer server = CreateServer(delegations: delegations);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 12,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult clientA = await client.Sessions.SetClientIdV40Async(
                                        "delegation-client-a",
                                        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult clientAConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        clientA.ClientId,
                                        clientA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "docs",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openA = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientA.ClientId,
                                        "delegation-owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openAConfirm = await client.Files.ConfirmOpenV40Async(
                                        openA.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40SetClientIdResult clientB = await client.Sessions.SetClientIdV40Async(
                                        "delegation-client-b",
                                        new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult clientBConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        clientB.ClientId,
                                        clientB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult delayedOpen = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientB.ClientId,
                                        "delegation-owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DelegationReturnResult returnDelegation = await client.Files.ReturnDelegationV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        openA.Delegation!.StateId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult retryOpen = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientB.ClientId,
                                        "delegation-owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!clientA.IsSuccess
                                        || !clientAConfirm.IsSuccess
                                        || !rootLookup.IsSuccess
                                        || !docsLookup.IsSuccess
                                        || !noteLookup.IsSuccess
                                        || !openA.IsSuccess
                                        || openA.Delegation is null
                                        || openA.Delegation.DelegationType != OpenNfsV40DelegationType.Read
                                        || !openAConfirm.IsSuccess
                                        || !clientB.IsSuccess
                                        || !clientBConfirm.IsSuccess
                                        || delayedOpen.Status != OpenNfsV40Status.Delay
                                        || !delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                        || !returnDelegation.IsSuccess
                                        || !delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                        || !retryOpen.IsSuccess)
                                    {
                                        throw new InvalidOperationException(
                                            "Expected grouped NFSv4.0 delegation APIs to grant a read delegation, force a conflicting client into DELAY, and succeed after DELEGRETURN."
                                            + " Observed:"
                                            + " clientA=" + clientA.Status
                                            + ", clientAConfirm=" + clientAConfirm.Status
                                            + ", openA=" + openA.Status
                                            + ", delegation=" + (openA.Delegation?.DelegationType.ToString() ?? "<null>")
                                            + ", openAConfirm=" + openAConfirm.Status
                                            + ", clientB=" + clientB.Status
                                            + ", clientBConfirm=" + clientBConfirm.Status
                                            + ", delayedOpen=" + delayedOpen.Status
                                            + ", wasRecalled=" + delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                            + ", recalls=" + delegations.DescribeRecalls()
                                            + ", returnDelegation=" + returnDelegation.Status
                                            + ", wasReturned=" + delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                            + ", returns=" + delegations.DescribeReturns()
                                            + ", retryOpen=" + retryOpen.Status
                                            + ".");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedDelegationApisNegative",
                        displayName: "Grouped NFSv4.0 delegation APIs preserve non-advertised and bad-state failures",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 7,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult session = await client.Sessions.SetClientIdV40Async(
                                        "delegation-negative-client",
                                        new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirm = await client.Sessions.ConfirmClientIdV40Async(
                                        session.ClientId,
                                        session.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "docs",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        session.ClientId,
                                        "delegation-negative-owner",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DelegationReturnResult badReturn = await client.Files.ReturnDelegationV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        new OpenNfsV40StateId(1U, new byte[12]),
                                        cancellationToken).ConfigureAwait(false);

                                    if (!session.IsSuccess
                                        || !confirm.IsSuccess
                                        || !openResult.IsSuccess
                                        || openResult.Delegation is not null
                                        || badReturn.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 delegation APIs to preserve non-advertisement and BAD_STATEID behavior when the server does not grant delegations.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
                });
        }

        private static OpenNfsCompoundOperation CreateLookupOperation(string entryName)
        {
            XdrWriter writer = new XdrWriter();
            new LOOKUP4args
            {
                objname = new component4
                {
                    Value = new utf8str_cs
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(entryName),
                        },
                    },
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_LOOKUP, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreatePutFileHandleOperation(byte[] fileHandle)
        {
            XdrWriter writer = new XdrWriter();
            new PUTFH4args
            {
                @object = new nfs_fh4
                {
                    Value = fileHandle,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTFH, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreatePutPublicFileHandleOperation()
        {
            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTPUBFH, Array.Empty<byte>());
        }

        private static OpenNfsCompoundOperation CreateVerifyOperation(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new VERIFY4args
            {
                obj_attributes = CreateTypeAttributes(fileType),
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_VERIFY, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreateNotVerifyOperation(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new NVERIFY4args
            {
                obj_attributes = CreateTypeAttributes(fileType),
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_NVERIFY, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreateOpenAttributeOperation(bool createdir)
        {
            XdrWriter writer = new XdrWriter();
            new OPENATTR4args
            {
                createdir = createdir,
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_OPENATTR, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreateDelegationPurgeOperation(ulong clientId)
        {
            XdrWriter writer = new XdrWriter();
            new DELEGPURGE4args
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_DELEGPURGE, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreateReleaseLockOwnerOperation(ulong clientId, byte[] ownerBytes)
        {
            XdrWriter writer = new XdrWriter();
            new RELEASE_LOCKOWNER4args
            {
                lock_owner = new lock_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = ownerBytes,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_RELEASE_LOCKOWNER, writer.ToArray());
        }

        private static OpenNfsCompoundOperation CreateReadDirectoryOperation(ulong cookie, byte[] cookieVerifier, uint maxCount)
        {
            XdrWriter writer = new XdrWriter();
            new READDIR4args
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                cookieverf = new verifier4
                {
                    Value = cookieVerifier,
                },
                dircount = new count4
                {
                    Value = maxCount,
                },
                maxcount = new count4
                {
                    Value = maxCount,
                },
                attr_request = new bitmap4
                {
                    Value = new[]
                    {
                        (1U << (int)Nfs40Constants.FATTR4_TYPE)
                        | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                        | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                        | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                    },
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_READDIR, writer.ToArray());
        }

        private static fattr4 CreateTypeAttributes(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new fattr4_type
            {
                Value = fileType,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_TYPE),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        private static OpenNfsServer CreateServer(
            TestNfsIdMapper? idMapper = null,
            TestNfsAcls? acls = null,
            TestNfsDelegations? delegations = null)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\exports\docs\shortcut"] = NfsPathKind.SymbolicLink,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\shortcut"] = "notes.txt",
                });

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports");

            if (idMapper is not null)
            {
                builder.UseIdMapper(idMapper);
            }

            if (acls is not null)
            {
                builder.UseAcls(acls);
            }

            if (delegations is not null)
            {
                builder.UseDelegations(delegations);
            }

            return builder.Build();
        }

        private static OpenNfsServer CreateCrossExportServer()
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\exports\docs\shortcut"] = NfsPathKind.SymbolicLink,
                    [@"C:\other"] = NfsPathKind.Directory,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\shortcut"] = "notes.txt",
                });

            return new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .AddExport("/other", @"C:\other")
                .Build();
        }

        private static async Task<(OpenNfsServer Server, NfsFileHandle DocsHandle, NfsFileHandle NoteHandle)> CreateLockingServerAsync(
            CancellationToken cancellationToken)
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                });

            await fileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    Encoding.UTF8.GetBytes("hello-v4"),
                    NfsWriteStability.FileSync,
                    cancellationToken)).ConfigureAwait(false);

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .Build();

            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                cancellationToken).ConfigureAwait(false);

            return (server, docsHandle, noteHandle);
        }

        private static async Task RunAgainstLoopbackServiceAsync(
            OpenNfsServer server,
            int expectedCallCount,
            Func<OpenNfsClient, Task> runClient,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            await RunAgainstLoopbackServiceAsync(
                new Nfs40CompoundService(server),
                expectedCallCount,
                runClient,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task RunAgainstLoopbackServiceAsync(
            Nfs40CompoundService service,
            int expectedCallCount,
            Func<OpenNfsClient, Task> runClient,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(runClient);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using CancellationTokenSource serverCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int actualCallCount = 0;
            ExceptionDispatchInfo? clientFailure = null;

            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task serverTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            while (!serverCancellationSource.IsCancellationRequested)
                            {
                                TcpClient acceptedClient = await listener.AcceptTcpClientAsync(serverCancellationSource.Token).ConfigureAwait(false);
                                _ = Task.Run(
                                    async () =>
                                    {
                                        using (acceptedClient)
                                        using (NetworkStream stream = acceptedClient.GetStream())
                                        {
                                            RpcTcpTransport transport = new RpcTcpTransport(
                                                stream,
                                                new RpcTransportOptions(
                                                    timeouts: new RpcTransportTimeouts(
                                                        readTimeout: TimeSpan.FromSeconds(30),
                                                        writeTimeout: TimeSpan.FromSeconds(5))));

                                            try
                                            {
                                                while (!serverCancellationSource.IsCancellationRequested)
                                                {
                                                    RpcMessageEnvelope request = await transport.ReceiveAsync(serverCancellationSource.Token).ConfigureAwait(false);
                                                    Interlocked.Increment(ref actualCallCount);
                                                    RpcMessageEnvelope reply = await service.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
                                                    await transport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                                                }
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }
                                    },
                                    CancellationToken.None);
                            }
                        }
                        catch (OperationCanceledException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                        catch (ObjectDisposedException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                        catch (SocketException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                    },
                    CancellationToken.None);

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                        .Build();
                    await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                    await runClient(client).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    clientFailure = ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    serverCancellationSource.Cancel();
                    listener.Stop();
                    await serverTask.ConfigureAwait(false);
                }

                if (clientFailure is null && actualCallCount != expectedCallCount)
                {
                    throw new InvalidOperationException($"Expected {expectedCallCount} NFSv4.0 loopback calls but observed {actualCallCount}.");
                }

                clientFailure?.Throw();
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
