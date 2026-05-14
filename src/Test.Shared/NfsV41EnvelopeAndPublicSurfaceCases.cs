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
    /// Public surface, status mapping, and compound-envelope NFSv4.1 suites.
    /// </summary>
    internal static class NfsV41EnvelopeAndPublicSurfaceCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "StatusExceptionMappingClassifiesNativeStatusCodes",
                        displayName: "OpenNfsV41StatusException maps native nfsstat4 codes to the documented OpenNfsErrorCategory values",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            EnsureCategory(nfsstat4.NFS4ERR_NOENT, OpenNfsErrorCategory.NotFound);
                            EnsureCategory(nfsstat4.NFS4ERR_ACCESS, OpenNfsErrorCategory.AccessDenied);
                            EnsureCategory(nfsstat4.NFS4ERR_PERM, OpenNfsErrorCategory.AccessDenied);
                            EnsureCategory(nfsstat4.NFS4ERR_EXIST, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_NOTDIR, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_ISDIR, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_NOTSUPP, OpenNfsErrorCategory.Unsupported);
                            EnsureCategory(nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH, OpenNfsErrorCategory.Unsupported);
                            EnsureCategory(nfsstat4.NFS4ERR_IO, OpenNfsErrorCategory.IoError);
                            EnsureCategory(nfsstat4.NFS4ERR_NOSPC, OpenNfsErrorCategory.IoError);
                            EnsureCategory(nfsstat4.NFS4ERR_BAD_SEQID, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_BADSESSION, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_BAD_STATEID, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_OP_NOT_IN_SESSION, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_STALE, OpenNfsErrorCategory.NotFound);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "GetOutcomeOrThrowSurfacesTypedExceptionOnPartial",
                        displayName: "OpenNfsV41CompoundResult.GetOutcomeOrThrow throws OpenNfsV41StatusException on partial-state COMPOUNDs and returns the outcome on full success",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 245, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult fullSuccess = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-throw-success",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            OpenNfsV41CompoundOutcome outcome = fullSuccess.GetOutcomeOrThrow("test-success");
                            if (outcome.Response.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("GetOutcomeOrThrow must return the outcome unchanged on full success.");
                            }

                            OpenNfsV41CompoundResult partial = await session.TrySendCompoundAsync(
                                operations: new[] { new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH } },
                                cacheReply: false,
                                tag: "envelope-throw-partial",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            try
                            {
                                partial.GetOutcomeOrThrow("test-partial");
                                throw new InvalidOperationException("GetOutcomeOrThrow must throw OpenNfsV41StatusException on a partial-state COMPOUND.");
                            }
                            catch (OpenNfsV41StatusException ex)
                            {
                                if (ex.Status != nfsstat4.NFS4ERR_NOTSUPP
                                    || ex.Category != OpenNfsErrorCategory.Unsupported
                                    || ex.OperationName != "test-partial"
                                    || ex.FailedOperationIndex < 0)
                                {
                                    throw new InvalidOperationException(
                                        "OpenNfsV41StatusException must carry status=NFS4ERR_NOTSUPP, category=Unsupported, the operation name, and the failing op index.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "GetOutcomeOrThrowRethrowsTransportFailure",
                        displayName: "OpenNfsV41CompoundResult.GetOutcomeOrThrow rethrows the transport-level failure when the call did not reach the server",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 246, cancellationToken).ConfigureAwait(false);

                            session.AbortConnectionForTest();

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-throw-transport",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            bool rethrown = false;
                            try
                            {
                                result.GetOutcomeOrThrow("test-transport");
                            }
                            catch (OpenNfsV41StatusException)
                            {
                                throw new InvalidOperationException(
                                    "GetOutcomeOrThrow must NOT surface a status exception when no outcome was received; it must rethrow the transport-level failure.");
                            }
                            catch (System.IO.EndOfStreamException)
                            {
                                rethrown = true;
                            }
                            catch (System.IO.IOException)
                            {
                                rethrown = true;
                            }
                            catch (SocketException)
                            {
                                rethrown = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                rethrown = true;
                            }
                            catch (OperationCanceledException)
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    rethrown = true;
                                }
                                else
                                {
                                    throw;
                                }
                            }

                            if (!rethrown)
                            {
                                throw new InvalidOperationException(
                                    "GetOutcomeOrThrow must rethrow a transport-level failure when the call did not reach the server.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PublicV41ClientSurfaceShapeIsPinned",
                        displayName: "Public NFSv4.1 client surface exposes the expected types and methods for the OpenCIFS-aligned compatibility shape",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            EnsurePublicType(typeof(OpenNfsV41ClientOwner));
                            EnsurePublicType(typeof(OpenNfsV41ClientSessionOptions));
                            EnsurePublicType(typeof(OpenNfsV41ClientSession));
                            EnsurePublicType(typeof(OpenNfsV41CompoundOutcome));
                            EnsurePublicType(typeof(OpenNfsV41CompoundResult));
                            EnsurePublicType(typeof(OpenNfsV41CallbackHandler));
                            EnsurePublicType(typeof(OpenNfsV41CallbackDispatcher));
                            EnsurePublicType(typeof(Nfs41CallbackChannelHost));
                            EnsurePublicType(typeof(OpenNfsV41PathOperations));
                            EnsurePublicType(typeof(OpenNfsV41MountSession));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionMetadata));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionFiles));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionDirectories));

                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.CreateMountSession));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Session));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Metadata));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Files));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Directories));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionMetadata), nameof(OpenNfsV41MountSessionMetadata.GetAttributesAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionFiles), nameof(OpenNfsV41MountSessionFiles.ReadAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionDirectories), nameof(OpenNfsV41MountSessionDirectories.ListAsync));

                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildAttributeMask));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildStatLikeAttributeMask));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.SplitPathComponents));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildGetAttributesOps));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildReadOps));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildReaddirOps));

                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.EstablishAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.SendCompoundAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.TrySendCompoundAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ReconnectAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.DisposeAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.IsSameServerInstance));

                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.SessionId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ClientId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.NegotiatedSlotCount));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerMajorId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerMinorId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerScope));

                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.AuthenticationFlavor));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.AuthSysCredentials));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.AutoReconnect));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.MaximumReconnectAttempts));

                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.IsFullSuccess));
                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.HasPartialResults));
                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.ReachedServer));

                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnRecallAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnGetAttributesAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnRecallAnyAsync));

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundFullSuccessSurfacesEnvelope",
                        displayName: "TrySendCompoundAsync surfaces a full-success envelope when every COMPOUND op completes",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 240, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-full-success",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (!result.IsFullSuccess
                                || result.HasPartialResults
                                || result.Failure is not null
                                || result.Outcome is null
                                || result.OperationsObservedSuccessfully != 1)
                            {
                                throw new InvalidOperationException(
                                    "A SEQUENCE-only COMPOUND must surface IsFullSuccess=true with HasPartialResults=false and one OK op.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundPartialResultsAfterUnsupportedOp",
                        displayName: "TrySendCompoundAsync surfaces a partial-success envelope when SEQUENCE succeeds but a follow-on op returns NFS4ERR_NOTSUPP",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 241, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
                                },
                                cacheReply: false,
                                tag: "envelope-partial",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (result.IsFullSuccess
                                || !result.HasPartialResults
                                || !result.ReachedServer
                                || result.Outcome is null
                                || result.Outcome.Response.status != nfsstat4.NFS4ERR_NOTSUPP
                                || result.OperationsObservedSuccessfully != 1
                                || result.Outcome.Response.resarray is null
                                || result.Outcome.Response.resarray.Length != 2
                                || result.Outcome.Response.resarray[0].opsequence?.sr_status != nfsstat4.NFS4_OK
                                || result.Outcome.Response.resarray[1].opillegal?.status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException(
                                    "A SEQUENCE+PUTROOTFH COMPOUND must surface HasPartialResults=true with the SEQUENCE OK and the unsupported op carrying NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundTransportFailureSurfacesEnvelope",
                        displayName: "TrySendCompoundAsync surfaces a transport-failure envelope when the underlying connection drops",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 242, cancellationToken).ConfigureAwait(false);

                            session.AbortConnectionForTest();

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-transport-fail",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (result.IsFullSuccess
                                || result.HasPartialResults
                                || result.ReachedServer
                                || result.Failure is null)
                            {
                                throw new InvalidOperationException(
                                    "A forced disconnect on a session without AutoReconnect must surface a transport-failure envelope.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionUsesConfiguredAuthSysCredentialsByDefault",
                        displayName: "OpenNfsV41ClientSession defaults to AUTH_SYS and routes configured AUTH_SYS identity values through EXCHANGE_ID, CREATE_SESSION, and SEQUENCE traffic",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                                "127.0.0.1",
                                host.NfsPort,
                                FaultInjectingRpcProxyMode.Transparent);

                            TestPrincipalIdentity expectedPrincipal = new TestPrincipalIdentity(
                                machineName: "v41-authsys-default",
                                userId: 1401U,
                                groupId: 1402U,
                                supplementaryGroupIds: new uint[] { 1403U, 1404U });
                            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                                endpoint: new IPEndPoint(IPAddress.Loopback, proxy.LocalPort),
                                clientOwner: BuildClientOwner(verifier: 0xE6, ownerSeed: 247));
                            options.AuthSysCredentials = new OpenNfsAuthSysCredentials(
                                expectedPrincipal.MachineName,
                                expectedPrincipal.UserId,
                                expectedPrincipal.GroupId,
                                expectedPrincipal.SupplementaryGroupIds);

                            await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                                .EstablishAsync(options, cancellationToken)
                                .ConfigureAwait(false);

                            _ = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "capture-authsys-default",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            CapturedRpcEnvelope[] firstThreeRequests = proxy.CapturedClientRequests.Take(3).ToArray();
                            if (firstThreeRequests.Length != 3)
                            {
                                throw new InvalidOperationException(
                                    "Expected OpenNfsV41ClientSession establishment plus the first session COMPOUND to emit three captured RPC requests.");
                            }

                            RpcCaptureAssertions.AssertCallRoutingSequence(
                                firstThreeRequests,
                                (uint)NFS4_PROGRAM_Program.Program,
                                (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                                (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                                (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                                (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND);

                            for (int index = 0; index < firstThreeRequests.Length; index++)
                            {
                                RpcCaptureAssertions.AssertAuthSysCredential(firstThreeRequests[index], expectedPrincipal);
                            }
                        }),

            };
        }
    }
}
