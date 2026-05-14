namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using static Test.Shared.NfsV40SuitePayloadSupport;
    using static Test.Shared.NfsV40SuiteRequestSupport;

    internal static class NfsV40SuiteSetupSupport
    {
        internal static async Task<LockingServiceContext> CreateLockingServiceAsync(
            CancellationToken cancellationToken,
            MutableClock? clock = null,
            TimeSpan? leaseWindow = null,
            TimeSpan? gracePeriodDuration = null)
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

            Nfs40CompoundService service = new Nfs40CompoundService(
                server,
                leaseWindow,
                gracePeriodDuration,
                clock is null ? null : clock.UtcNow);
            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                cancellationToken).ConfigureAwait(false);

            return new LockingServiceContext(server, service, docsHandle, notesHandle);
        }

        internal static async Task<ClientSessionContext> CreateClientSessionAsync(
            Nfs40CompoundService service,
            string clientIdentifier,
            byte[] clientVerifier,
            uint xidBase,
            CancellationToken cancellationToken)
        {
            COMPOUND4res setClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase,
                        clientIdentifier + "-setclientid",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID,
                                opsetclientid = CreateSetClientIdArguments(clientIdentifier, clientVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid and confirmation verifier.");
            ulong clientId = setClientIdResok.clientid?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid.");
            byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier.");

            if (setClientIdResult.status != nfsstat4.NFS4_OK || confirmationVerifier.Length != 8)
            {
                throw new InvalidOperationException("Expected SETCLIENTID to succeed and return an eight-byte confirmation verifier.");
            }

            COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 1U,
                        clientIdentifier + "-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed.");
            }

            return new ClientSessionContext(clientId, confirmationVerifier);
        }

        internal static async Task<ConfirmedOpenStateContext> CreateConfirmedOpenStateForClientAsync(
            Nfs40CompoundService service,
            NfsFileHandle docsHandle,
            string clientIdentifier,
            byte[] clientVerifier,
            string openOwner,
            uint xidBase,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(docsHandle);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientIdentifier);
            ArgumentNullException.ThrowIfNull(clientVerifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(openOwner);

            COMPOUND4res setClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase,
                        clientIdentifier + "-setclientid",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID,
                                opsetclientid = CreateSetClientIdArguments(clientIdentifier, clientVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid and confirmation verifier for the lock setup flow.");
            ulong clientId = setClientIdResok.clientid?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid for the lock setup flow.");
            byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier for the lock setup flow.");

            if (setClientIdResult.status != nfsstat4.NFS4_OK || confirmationVerifier.Length != 8)
            {
                throw new InvalidOperationException("Expected successful lock setup SETCLIENTID to return an eight-byte confirmation verifier.");
            }

            COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 1U,
                        clientIdentifier + "-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed for the lock setup flow.");
            }

            COMPOUND4res openResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 2U,
                        clientIdentifier + "-open",
                        0U,
                        new[]
                        {
                            CreatePutFileHandleArgop(docsHandle),
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_OPEN,
                                opopen = CreateOpenExistingArguments(
                                    clientId,
                                    openOwner,
                                    1U,
                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                    "notes.txt"),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            stateid4 openStateId = openResult.resarray?[1].opopen?.resok4?.stateid
                ?? throw new InvalidOperationException("Expected OPEN to return a stateid for the lock setup flow.");

            if (openResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected OPEN to succeed for the lock setup flow.");
            }

            COMPOUND4res openConfirmResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 3U,
                        clientIdentifier + "-open-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                opopen_confirm = new OPEN_CONFIRM4args
                                {
                                    open_stateid = openStateId,
                                    seqid = CreateSequenceId(2U),
                                },
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            stateid4 confirmedStateId = openConfirmResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                ?? throw new InvalidOperationException("Expected OPEN_CONFIRM to return the confirmed stateid for the lock setup flow.");

            if (openConfirmResult.status != nfsstat4.NFS4_OK || confirmedStateId.seqid != 2U)
            {
                throw new InvalidOperationException("Expected OPEN_CONFIRM to advance the stateid sequence during the lock setup flow.");
            }

            return new ConfirmedOpenStateContext(clientId, confirmedStateId);
        }
    }
}
