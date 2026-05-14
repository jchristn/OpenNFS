namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RecordMarking;
    using OpenNFS.Rpc.RpcMessages;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.RpcTransportSuiteSupport;

    /// <summary>
    /// RPC envelope and record-marking codec suites.
    /// </summary>
    internal static class RpcTransportEnvelopeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "RecordMarkingFragmentation",
                    displayName: "Record marking fragments and reassembles call messages",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        RpcGenerated.authsys_parms expectedSystemCredential = new RpcGenerated.authsys_parms
                        {
                            stamp = 99,
                            machinename = "transport-client",
                            uid = 501,
                            gid = 20,
                            gids = new uint[] { 30, 31, 32 },
                        };

                        RpcMessageEnvelope expectedEnvelope = RpcMessageFactory.CreateCall(
                            xid: 0x01020304,
                            program: 100003,
                            version: 3,
                            procedure: 1,
                            credential: RpcAuthenticationCodec.CreateSystem(expectedSystemCredential),
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0 });

                        byte[] encodedMessage = RpcMessageCodec.Encode(expectedEnvelope);
                        byte[] recordMarkedMessage = RecordMarkingCodec.EncodeMessage(encodedMessage, maximumFragmentPayloadLength: 9);

                        IReadOnlyList<RecordMarkingFragmentHeader> fragmentHeaders = ReadFragmentHeaders(recordMarkedMessage);
                        if (fragmentHeaders.Count < 2)
                        {
                            throw new InvalidOperationException("Expected record marking to split the call message across multiple fragments.");
                        }

                        for (int index = 0; index < fragmentHeaders.Count - 1; index++)
                        {
                            if (fragmentHeaders[index].IsLastFragment)
                            {
                                throw new InvalidOperationException("Only the final record-marking fragment may be flagged as the last fragment.");
                            }
                        }

                        RecordMarkingFragmentHeader finalHeader = fragmentHeaders[fragmentHeaders.Count - 1];
                        if (!finalHeader.IsLastFragment)
                        {
                            throw new InvalidOperationException("Expected the final record-marking fragment to terminate the record.");
                        }

                        byte[] decodedMessage = RecordMarkingCodec.DecodeSingleMessage(recordMarkedMessage);
                        RpcMessageEnvelope actualEnvelope = RpcMessageCodec.Decode(decodedMessage);

                        RpcGenerated.rpc_msg_body? actualBody = actualEnvelope.Header.body;
                        if (actualBody?.mtype != RpcGenerated.msg_type.CALL)
                        {
                            throw new InvalidOperationException("Expected the reassembled RPC envelope to remain a CALL message.");
                        }

                        RpcGenerated.call_body? actualCallBody = actualBody.cbody;
                        if (actualCallBody is null)
                        {
                            throw new InvalidOperationException("Expected the reassembled RPC envelope to retain a call body.");
                        }

                        if (actualCallBody.rpcvers != RpcProtocolConstants.RpcVersion
                            || actualCallBody.prog != 100003
                            || actualCallBody.vers != 3
                            || actualCallBody.proc != 1)
                        {
                            throw new InvalidOperationException("Expected the reassembled call header to preserve program routing fields.");
                        }

                        RpcGenerated.authsys_parms actualSystemCredential = RpcAuthenticationCodec.ReadSystem(
                            actualCallBody.cred ?? throw new InvalidOperationException("Expected the call credential to remain present."));

                        if (actualSystemCredential.stamp != expectedSystemCredential.stamp
                            || !string.Equals(actualSystemCredential.machinename, expectedSystemCredential.machinename, StringComparison.Ordinal)
                            || actualSystemCredential.uid != expectedSystemCredential.uid
                            || actualSystemCredential.gid != expectedSystemCredential.gid
                            || actualSystemCredential.gids is null
                            || !actualSystemCredential.gids.SequenceEqual(expectedSystemCredential.gids ?? Array.Empty<uint>()))
                        {
                            throw new InvalidOperationException("Expected the AUTH_SYS credential payload to round-trip through record marking intact.");
                        }

                        if (!actualEnvelope.ProcedurePayload.Span.SequenceEqual(expectedEnvelope.ProcedurePayload.Span))
                        {
                            throw new InvalidOperationException("Expected the call procedure payload to survive fragmentation and reassembly intact.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "AcceptedReplyRoundTrip",
                    displayName: "Accepted replies round-trip through the RPC envelope codec",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        RpcMessageEnvelope expectedSuccessReply = RpcMessageFactory.CreateAcceptedReply(
                            xid: 0x11112222,
                            status: RpcGenerated.accept_stat.SUCCESS,
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

                        RpcMessageEnvelope actualSuccessReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedSuccessReply));
                        RpcGenerated.reply_body? successReplyBody = actualSuccessReply.Header.body?.rbody;
                        if (successReplyBody?.stat != RpcGenerated.reply_stat.MSG_ACCEPTED)
                        {
                            throw new InvalidOperationException("Expected the successful reply to remain an accepted reply.");
                        }

                        RpcGenerated.accepted_reply_reply_data? successReplyData = successReplyBody.areply?.reply_data;
                        if (successReplyData?.stat != RpcGenerated.accept_stat.SUCCESS)
                        {
                            throw new InvalidOperationException("Expected the successful reply to preserve SUCCESS status.");
                        }

                        if (successReplyData.results is null || successReplyData.results.Length != 0)
                        {
                            throw new InvalidOperationException("Expected successful replies to preserve the zero-length results sentinel.");
                        }

                        if (!actualSuccessReply.ProcedurePayload.Span.SequenceEqual(expectedSuccessReply.ProcedurePayload.Span))
                        {
                            throw new InvalidOperationException("Expected successful accepted replies to preserve their procedure result payload.");
                        }

                        RpcMessageEnvelope expectedMismatchReply = RpcMessageFactory.CreateAcceptedReply(
                            xid: 0x33334444,
                            status: RpcGenerated.accept_stat.PROG_MISMATCH,
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            mismatchLowVersion: 3,
                            mismatchHighVersion: 4);

                        RpcMessageEnvelope actualMismatchReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedMismatchReply));
                        RpcGenerated.accepted_reply_reply_data_mismatch_info? mismatchInfo =
                            actualMismatchReply.Header.body?.rbody?.areply?.reply_data?.mismatch_info;

                        if (actualMismatchReply.Header.body?.rbody?.areply?.reply_data?.stat != RpcGenerated.accept_stat.PROG_MISMATCH
                            || mismatchInfo is null
                            || mismatchInfo.low != 3
                            || mismatchInfo.high != 4)
                        {
                            throw new InvalidOperationException("Expected PROG_MISMATCH accepted replies to preserve version mismatch information.");
                        }

                        if (actualMismatchReply.ProcedurePayload.Length != 0)
                        {
                            throw new InvalidOperationException("Expected PROG_MISMATCH accepted replies to remain payload-free.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "RejectedReplyRoundTrip",
                    displayName: "Rejected replies round-trip through the RPC envelope codec",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        RpcMessageEnvelope expectedRpcMismatchReply = RpcMessageFactory.CreateRejectedReply(
                            xid: 0x55556666,
                            status: RpcGenerated.reject_stat.RPC_MISMATCH,
                            mismatchLowVersion: 2,
                            mismatchHighVersion: 4);

                        RpcMessageEnvelope actualRpcMismatchReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedRpcMismatchReply));
                        RpcGenerated.rejected_reply_mismatch_info? rpcMismatchInfo =
                            actualRpcMismatchReply.Header.body?.rbody?.rreply?.mismatch_info;

                        if (actualRpcMismatchReply.Header.body?.rbody?.stat != RpcGenerated.reply_stat.MSG_DENIED
                            || actualRpcMismatchReply.Header.body?.rbody?.rreply?.stat != RpcGenerated.reject_stat.RPC_MISMATCH
                            || rpcMismatchInfo is null
                            || rpcMismatchInfo.low != 2
                            || rpcMismatchInfo.high != 4)
                        {
                            throw new InvalidOperationException("Expected RPC_MISMATCH rejected replies to preserve version mismatch information.");
                        }

                        if (actualRpcMismatchReply.ProcedurePayload.Length != 0)
                        {
                            throw new InvalidOperationException("Expected RPC_MISMATCH replies to remain payload-free.");
                        }

                        RpcMessageEnvelope expectedAuthErrorReply = RpcMessageFactory.CreateRejectedReply(
                            xid: 0x77778888,
                            status: RpcGenerated.reject_stat.AUTH_ERROR,
                            authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED);

                        RpcMessageEnvelope actualAuthErrorReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedAuthErrorReply));
                        RpcGenerated.auth_stat? authStatus = actualAuthErrorReply.Header.body?.rbody?.rreply?.stat_value;

                        if (actualAuthErrorReply.Header.body?.rbody?.rreply?.stat != RpcGenerated.reject_stat.AUTH_ERROR
                            || authStatus != RpcGenerated.auth_stat.AUTH_BADCRED)
                        {
                            throw new InvalidOperationException("Expected AUTH_ERROR rejected replies to preserve the authentication failure status.");
                        }

                        if (actualAuthErrorReply.ProcedurePayload.Length != 0)
                        {
                            throw new InvalidOperationException("Expected AUTH_ERROR replies to remain payload-free.");
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
