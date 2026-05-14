namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NlmSuiteSupport;

    /// <summary>
    /// Grouped-client NLM v4 execution and transport suites.
    /// </summary>
    internal static class NlmGroupedClientCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisExecutePositive",
                        displayName: "Grouped client lock APIs execute and decode typed NLM v4 replies",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            ScriptedRpcExecutor executor = new ScriptedRpcExecutor(
                                (request, _, token) => service.DispatchAsync(request.CallEnvelope, token));
                            OpenNfsClient client = CreateClient(executor);
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan testPlan = await client.Locks.PrepareTestV4Async(
                                new byte[] { 0x21 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                exclusive: false,
                                cancellationToken).ConfigureAwait(false);
                            if (testPlan.ProgramNumber != 100021
                                || testPlan.VersionNumber != 4
                                || testPlan.ProcedureNumber != 1)
                            {
                                throw new InvalidOperationException("Expected the grouped lock API to plan an NLM v4 TEST call.");
                            }

                            OpenNfsNlmV4Result lockResult = await client.Locks.LockV4Async(
                                new byte[] { 0x22 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);
                            if (!lockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 LOCK execution to succeed.");
                            }

                            OpenNfsNlmV4Result unlockResult = await client.Locks.UnlockV4Async(
                                new byte[] { 0x22 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                cancellationToken).ConfigureAwait(false);
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 UNLOCK execution to succeed.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisNegative",
                        displayName: "Grouped client lock APIs surface denied replies and malformed payload failures",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x3001,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        new byte[] { 0x31 },
                                        fileHandle,
                                        new byte[] { 0xE1 },
                                        "holder",
                                        601,
                                        4,
                                        12,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            ScriptedRpcExecutor executor = new ScriptedRpcExecutor(
                                async (request, _, token) =>
                                {
                                    if (request.OperationName == "NLM v4 TEST")
                                    {
                                        return await service.DispatchAsync(request.CallEnvelope, token).ConfigureAwait(false);
                                    }

                                    return RpcMessageFactory.CreateAcceptedReply(
                                        xid: request.CallEnvelope.Header.xid,
                                        status: accept_stat.SUCCESS,
                                        verifier: RpcAuthenticationCodec.CreateNone(),
                                        procedurePayload: new byte[] { 0x00, 0x00, 0x00, 0x01 });
                                });

                            OpenNfsClient client = CreateClient(executor);
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult deniedResult = await client.Locks.TestV4Async(
                                new byte[] { 0x32 },
                                fileHandle,
                                new byte[] { 0xE2 },
                                "other",
                                602,
                                4,
                                12,
                                exclusive: true,
                                cancellationToken).ConfigureAwait(false);

                            if (deniedResult.Status != OpenNfsNlmV4Status.Denied
                                || deniedResult.ConflictingHolder is null)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 TEST execution to surface a denied conflict.");
                            }

                            bool sawProtocolFailure = false;
                            try
                            {
                                _ = await client.Locks.LockV4Async(
                                    new byte[] { 0x33 },
                                    fileHandle,
                                    new byte[] { 0xE3 },
                                    "malformed",
                                    603,
                                    0,
                                    8,
                                    block: false,
                                    exclusive: true,
                                    reclaim: false,
                                    state: 0,
                                    cancellationToken).ConfigureAwait(false);
                            }
                            catch (OpenNfsClientProtocolException exception)
                            {
                                if (exception.Category != OpenNfsErrorCategory.ProtocolError
                                    || !exception.Message.Contains("malformed", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NLM v4 malformed-payload failures to surface a typed protocol exception.");
                                }

                                sawProtocolFailure = true;
                            }

                            if (!sawProtocolFailure)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 LOCK execution to fail on a malformed payload with a typed protocol exception.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisOverTcpPositive",
                        displayName: "Grouped client lock APIs execute over the real TCP NLM listener",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                            await using OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("127.0.0.1", host.NlmPort)
                                .Build();
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result lockResult = await client.Locks.LockV4Async(
                                new byte[] { 0x81 },
                                fileHandle,
                                new byte[] { 0x91 },
                                "tcp-owner",
                                701,
                                0,
                                48,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);
                            if (!lockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM lock to succeed.");
                            }

                            OpenNfsNlmV4Result unlockResult = await client.Locks.UnlockV4Async(
                                new byte[] { 0x81 },
                                fileHandle,
                                new byte[] { 0x91 },
                                "tcp-owner",
                                701,
                                0,
                                48,
                                cancellationToken).ConfigureAwait(false);
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM unlock to succeed.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisOverTcpNegative",
                        displayName: "Grouped client lock APIs surface denied conflicts over the real TCP NLM listener",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                            await using OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("127.0.0.1", host.NlmPort)
                                .Build();
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            _ = await client.Locks.LockV4Async(
                                new byte[] { 0x82 },
                                fileHandle,
                                new byte[] { 0x92 },
                                "holder",
                                702,
                                4,
                                20,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult deniedResult = await client.Locks.TestV4Async(
                                new byte[] { 0x83 },
                                fileHandle,
                                new byte[] { 0x93 },
                                "other",
                                703,
                                4,
                                20,
                                exclusive: true,
                                cancellationToken).ConfigureAwait(false);

                            if (deniedResult.Status != OpenNfsNlmV4Status.Denied
                                || deniedResult.ConflictingHolder is null
                                || !deniedResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(new byte[] { 0x92 }))
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM test to surface the held conflicting lock.");
                            }
                        }),
            };
        }
    }
}
