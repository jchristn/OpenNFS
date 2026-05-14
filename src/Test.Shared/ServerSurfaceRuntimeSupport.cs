namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using NfsV41Generated = OpenNFS.Protocol.V41.Generated;
    using NfsV42Generated = OpenNFS.Protocol.V42.Generated;

    /// <summary>
    /// Shared NFSv3 and NFSv4.0 verification helpers for the public server surface suites.
    /// </summary>
    internal static class ServerSurfaceRuntimeSupport
    {
        internal static async Task AssertServerApplicationServesV41SessionFlowAsync(
            string host,
            int nfs41Port,
            CancellationToken cancellationToken)
        {
            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                new IPEndPoint(IPAddress.Parse(host), nfs41Port),
                new OpenNfsV41ClientOwner(
                    verifier: new byte[] { 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51 },
                    ownerId: new byte[] { 0x41, 0x70, 0x70, 0x56, 0x34, 0x31 }));
            options.RequestedSlots = 4;
            options.ConnectTimeout = TimeSpan.FromSeconds(60);
            options.CallTimeout = TimeSpan.FromSeconds(60);

            await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                .EstablishAsync(options, cancellationToken)
                .ConfigureAwait(false);

            if (session.SessionId.Count != 16
                || session.ClientId == 0
                || session.NegotiatedSlotCount == 0)
            {
                throw new InvalidOperationException(
                    "Expected the public server application surface to establish a usable NFSv4.1 session-management connection.");
            }

            OpenNfsV41CompoundOutcome firstOutcome = await session.SendCompoundAsync(
                operations: Array.Empty<NfsV41Generated.nfs_argop4>(),
                cacheReply: false,
                tag: "server-surface-v41-1",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (firstOutcome.Response.status != NfsV41Generated.nfsstat4.NFS4_OK
                || firstOutcome.SlotId != 0
                || firstOutcome.SequenceId != 1)
            {
                throw new InvalidOperationException(
                    "Expected the first public NFSv4.1 session-management COMPOUND to succeed on slot 0 with sequenceid 1.");
            }

            OpenNfsV41CompoundOutcome secondOutcome = await session.SendCompoundAsync(
                operations: Array.Empty<NfsV41Generated.nfs_argop4>(),
                cacheReply: false,
                tag: "server-surface-v41-2",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (secondOutcome.Response.status != NfsV41Generated.nfsstat4.NFS4_OK
                || secondOutcome.SequenceId != 2)
            {
                throw new InvalidOperationException(
                    "Expected the second public NFSv4.1 session-management COMPOUND to advance the per-slot sequenceid to 2.");
            }
        }

        internal static async Task AssertServerApplicationServesV42IoAdviseFlowAsync(
            string host,
            int nfs42Port,
            CancellationToken cancellationToken)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(host), nfs42Port, cancellationToken).ConfigureAwait(false);

            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                RpcTcpTransport transport = new RpcTcpTransport(
                    stream,
                    new RpcTransportOptions(
                        timeouts: new RpcTransportTimeouts(
                            readTimeout: TimeSpan.FromSeconds(60),
                            writeTimeout: TimeSpan.FromSeconds(60))));

                NfsV42Generated.COMPOUND4args bootstrap = new NfsV42Generated.COMPOUND4args
                {
                    tag = MakeTag("server-surface-v42-bootstrap"),
                    minorversion = 2,
                    argarray = new[]
                    {
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_EXCHANGE_ID,
                            opexchange_id = BuildExchangeIdArguments(0x62, 0x42),
                        },
                    },
                };
                NfsV42Generated.COMPOUND4res bootstrapResponse = await SendCompoundV42Async(transport, bootstrap, xid: 4201, cancellationToken).ConfigureAwait(false);
                if (bootstrapResponse.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || bootstrapResponse.resarray is null
                    || bootstrapResponse.resarray.Length != 1
                    || bootstrapResponse.resarray[0].opexchange_id?.eir_resok4?.eir_clientid is null
                    || bootstrapResponse.resarray[0].opexchange_id?.eir_resok4?.eir_sequenceid is null)
                {
                    throw new InvalidOperationException("Expected the public server application surface to accept a real NFSv4.2 EXCHANGE_ID flow.");
                }

                NfsV42Generated.EXCHANGE_ID4resok bootstrapResult = bootstrapResponse.resarray[0].opexchange_id!.eir_resok4!;
                ulong clientId = bootstrapResult.eir_clientid!.Value;
                uint createSequenceId = bootstrapResult.eir_sequenceid!.Value;

                NfsV42Generated.COMPOUND4args createSession = new NfsV42Generated.COMPOUND4args
                {
                    tag = MakeTag("server-surface-v42-create"),
                    minorversion = 2,
                    argarray = new[]
                    {
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_CREATE_SESSION,
                            opcreate_session = BuildCreateSessionArguments(clientId, createSequenceId, requestedSlots: 4),
                        },
                    },
                };
                NfsV42Generated.COMPOUND4res createSessionResponse = await SendCompoundV42Async(transport, createSession, xid: 4202, cancellationToken).ConfigureAwait(false);
                if (createSessionResponse.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || createSessionResponse.resarray is null
                    || createSessionResponse.resarray.Length != 1
                    || createSessionResponse.resarray[0].opcreate_session?.csr_resok4?.csr_sessionid?.Value is null)
                {
                    throw new InvalidOperationException("Expected the public server application surface to create a usable NFSv4.2 session.");
                }

                byte[] sessionId = createSessionResponse.resarray[0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;

                NfsV42Generated.COMPOUND4args firstCompound = new NfsV42Generated.COMPOUND4args
                {
                    tag = MakeTag("server-surface-v42-1"),
                    minorversion = 2,
                    argarray = new[]
                    {
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_SEQUENCE,
                            opsequence = BuildSequenceArguments(sessionId, slotId: 0, sequenceId: 1, cacheThis: false, highestSlotId: 0),
                        },
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_PUTROOTFH,
                        },
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_LOOKUP,
                            oplookup = BuildLookupArguments("hello.txt"),
                        },
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_IO_ADVISE,
                            opio_advise = new NfsV42Generated.IO_ADVISE4args
                            {
                                iaa_stateid = new NfsV42Generated.stateid4 { seqid = 1, other = new byte[12] },
                                iaa_offset = new NfsV42Generated.offset4 { Value = 0 },
                                iaa_count = new NfsV42Generated.length4 { Value = 4096 },
                                iaa_hints = new NfsV42Generated.bitmap4 { Value = Array.Empty<uint>() },
                            },
                        },
                    },
                };
                NfsV42Generated.COMPOUND4res firstResponse = await SendCompoundV42Async(transport, firstCompound, xid: 4203, cancellationToken).ConfigureAwait(false);
                if (firstResponse.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || firstResponse.resarray is null
                    || firstResponse.resarray.Length != 4
                    || firstResponse.resarray[0].opsequence?.sr_resok4?.sr_sequenceid?.Value != 1
                    || firstResponse.resarray[1].opputrootfh?.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || firstResponse.resarray[2].oplookup?.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || firstResponse.resarray[3].opio_advise?.ior_status != NfsV42Generated.nfsstat4.NFS4_OK)
                {
                    throw new InvalidOperationException("Expected the public server application surface to execute a real NFSv4.2 SEQUENCE + PUTROOTFH + LOOKUP + IO_ADVISE COMPOUND.");
                }

                NfsV42Generated.COMPOUND4args secondCompound = new NfsV42Generated.COMPOUND4args
                {
                    tag = MakeTag("server-surface-v42-2"),
                    minorversion = 2,
                    argarray = new[]
                    {
                        new NfsV42Generated.nfs_argop4
                        {
                            argop = NfsV42Generated.nfs_opnum4.OP_SEQUENCE,
                            opsequence = BuildSequenceArguments(sessionId, slotId: 0, sequenceId: 2, cacheThis: false, highestSlotId: 0),
                        },
                    },
                };
                NfsV42Generated.COMPOUND4res secondResponse = await SendCompoundV42Async(transport, secondCompound, xid: 4204, cancellationToken).ConfigureAwait(false);
                if (secondResponse.status != NfsV42Generated.nfsstat4.NFS4_OK
                    || secondResponse.resarray is null
                    || secondResponse.resarray.Length != 1
                    || secondResponse.resarray[0].opsequence?.sr_resok4?.sr_sequenceid?.Value != 2)
                {
                    throw new InvalidOperationException("Expected the second public NFSv4.2 session-management COMPOUND to advance the per-slot sequenceid to 2.");
                }
            }
        }

        internal static async Task AssertServerApplicationServesV3AndV40FlowsAsync(
            string host,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            string exportPath,
            string v40ExportLeafName,
            string expectedHelloContents,
            CancellationToken cancellationToken)
        {
            await using OpenNfsClient nfs40Client = new OpenNfsClientBuilder()
                .WithServer(host, nfs40Port)
                .Build();
            await nfs40Client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            byte[] exportRootHandle = await ResolveExportRootV40Async(
                nfs40Client,
                v40ExportLeafName,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadDirectoryResult rootDirectoryResult = await nfs40Client.Directories.ReadDirectoryV40Async(
                exportRootHandle,
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            string[] rootNames = rootDirectoryResult.Entries.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            if (!rootDirectoryResult.IsSuccess
                || !rootNames.Contains("docs", StringComparer.Ordinal)
                || !rootNames.Contains("hello.txt", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to expose 'docs' and 'hello.txt' over the NFSv4.0 root flow.");
            }

            OpenNfsV40LookupResult helloLookupResult = await nfs40Client.Directories.LookupV40Async(
                exportRootHandle,
                "hello.txt",
                cancellationToken).ConfigureAwait(false);
            if (!helloLookupResult.IsSuccess || helloLookupResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the public server application surface to resolve 'hello.txt' over the NFSv4.0 direct flow.");
            }

            OpenNfsV40ReadResult helloReadResult = await nfs40Client.Files.ReadV40Async(
                helloLookupResult.ObjectFileHandle.ToArray(),
                0UL,
                128U,
                cancellationToken).ConfigureAwait(false);
            if (!helloReadResult.IsSuccess
                || !string.Equals(Encoding.UTF8.GetString(helloReadResult.Data.Span), expectedHelloContents, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to return the seeded hello.txt content over NFSv4.0.");
            }

            await using OpenNfsClient nfsV3Client = new OpenNfsClientBuilder()
                .WithServer(host, nfsPort)
                .WithMountPort(mountPort)
                .Build();
            await nfsV3Client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            await using OpenNfsMountSession session =
                await nfsV3Client.MountAsync(exportPath, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<OpenNfsV3DirectoryEntry> sessionRootEntries =
                await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
            string[] sessionRootNames = sessionRootEntries.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            if (!sessionRootNames.Contains("docs", StringComparer.Ordinal)
                || !sessionRootNames.Contains("hello.txt", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to expose 'docs' and 'hello.txt' over the mounted NFSv3 flow.");
            }

            byte[] helloBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);
            if (!string.Equals(Encoding.UTF8.GetString(helloBytes), expectedHelloContents, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to return the seeded hello.txt content over the mounted NFSv3 flow.");
            }

            await session.Directories.CreateFileAsync("/notes.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(
                "/notes.txt",
                Encoding.UTF8.GetBytes("written-through-application"),
                OpenNfsWriteStability.FileSync,
                cancellationToken).ConfigureAwait(false);
            byte[] writtenBytes = await session.Files.ReadAllBytesAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
            if (!string.Equals(Encoding.UTF8.GetString(writtenBytes), "written-through-application", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to round-trip NFSv3 write traffic.");
            }

            await session.Directories.DeleteFileAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<byte[]> ResolveExportRootV40Async(
            OpenNfsClient client,
            string exportLeafName,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the NFSv4.0 root discovery flow to return a usable filehandle.");
            }

            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            if (!rootListing.IsSuccess)
            {
                throw new InvalidOperationException("Expected the NFSv4.0 root directory listing to succeed.");
            }

            bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                static entry => string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                    || string.Equals(entry.Name, "hello.txt", StringComparison.Ordinal));
            if (rootAlreadyLooksLikeExport)
            {
                return rootResult.ObjectFileHandle.ToArray();
            }

            OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                exportLeafName,
                cancellationToken).ConfigureAwait(false);
            if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException(
                    "Expected the NFSv4.0 pseudo-root to expose export leaf '" + exportLeafName + "'.");
            }

            return exportLookup.ObjectFileHandle.ToArray();
        }

        private static NfsV42Generated.utf8str_cs MakeTag(string text)
        {
            return new NfsV42Generated.utf8str_cs
            {
                Value = new NfsV42Generated.utf8string { Value = Encoding.UTF8.GetBytes(text) },
            };
        }

        private static NfsV42Generated.EXCHANGE_ID4args BuildExchangeIdArguments(byte verifierByte, byte ownerSeed)
        {
            return new NfsV42Generated.EXCHANGE_ID4args
            {
                eia_clientowner = new NfsV42Generated.client_owner4
                {
                    co_verifier = new NfsV42Generated.verifier4
                    {
                        Value = new[]
                        {
                            verifierByte, verifierByte, verifierByte, verifierByte,
                            verifierByte, verifierByte, verifierByte, verifierByte,
                        },
                    },
                    co_ownerid = new[] { ownerSeed, (byte)0x10, (byte)0x20, (byte)0x30 },
                },
                eia_flags = 0,
                eia_state_protect = new NfsV42Generated.state_protect4_a
                {
                    spa_how = NfsV42Generated.state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<NfsV42Generated.nfs_impl_id4>(),
            };
        }

        private static NfsV42Generated.CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            NfsV42Generated.channel_attrs4 channelAttributes = new NfsV42Generated.channel_attrs4
            {
                ca_headerpadsize = new NfsV42Generated.count4 { Value = 0 },
                ca_maxrequestsize = new NfsV42Generated.count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new NfsV42Generated.count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new NfsV42Generated.count4 { Value = 64 * 1024 },
                ca_maxoperations = new NfsV42Generated.count4 { Value = 16 },
                ca_maxrequests = new NfsV42Generated.count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new NfsV42Generated.CREATE_SESSION4args
            {
                csa_clientid = new NfsV42Generated.clientid4 { Value = clientId },
                csa_sequence = new NfsV42Generated.sequenceid4 { Value = sequenceId },
                csa_flags = 0,
                csa_fore_chan_attrs = channelAttributes,
                csa_back_chan_attrs = channelAttributes,
                csa_cb_program = 0x40000000u,
                csa_sec_parms = Array.Empty<NfsV42Generated.callback_sec_parms4>(),
            };
        }

        private static NfsV42Generated.SEQUENCE4args BuildSequenceArguments(
            byte[] sessionIdBytes,
            uint slotId,
            uint sequenceId,
            bool cacheThis,
            uint highestSlotId)
        {
            return new NfsV42Generated.SEQUENCE4args
            {
                sa_sessionid = new NfsV42Generated.sessionid4 { Value = sessionIdBytes },
                sa_sequenceid = new NfsV42Generated.sequenceid4 { Value = sequenceId },
                sa_slotid = new NfsV42Generated.slotid4 { Value = slotId },
                sa_highest_slotid = new NfsV42Generated.slotid4 { Value = highestSlotId },
                sa_cachethis = cacheThis,
            };
        }

        private static NfsV42Generated.LOOKUP4args BuildLookupArguments(string entryName)
        {
            return new NfsV42Generated.LOOKUP4args
            {
                objname = new NfsV42Generated.component4
                {
                    Value = new NfsV42Generated.utf8str_cs
                    {
                        Value = new NfsV42Generated.utf8string { Value = Encoding.UTF8.GetBytes(entryName) },
                    },
                },
            };
        }

        private static async Task<NfsV42Generated.COMPOUND4res> SendCompoundV42Async(
            RpcTcpTransport transport,
            NfsV42Generated.COMPOUND4args arguments,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NfsV42Generated.NFS4_PROGRAM_Program.Program,
                version: (uint)NfsV42Generated.NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NfsV42Generated.NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Real-network NFSv4.2 COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            XdrReader reader = new XdrReader(reply.ProcedurePayload);
            NfsV42Generated.COMPOUND4res value = NfsV42Generated.COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return value;
        }
    }
}
