namespace Test.Shared
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// README, packaging, and compatibility metadata helpers for the public client surface suites.
    /// </summary>
    internal static class ClientSurfacePackagingSupport
    {
        internal static async Task ExecutePackedClientPackageSupportsRpcSecGssConfigurationFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using OpenNFS.Client;

public static class Program
{
    public static int Main()
    {
        OpenNfsClientSettings settings = new OpenNfsClientBuilder()
            .WithServer("sample.example.test", 2049)
            .WithMountPort(20048)
            .WithRpcSecGssKerberos("nfs/sample.example.test@EXAMPLE.TEST")
            .BuildSettings();

        if (settings.AuthenticationFlavor != OpenNfsAuthenticationFlavor.RpcSecGss)
        {
            throw new InvalidOperationException("Expected WithRpcSecGssKerberos to promote RPCSEC_GSS as the active client authentication flavor.");
        }

        if (settings.RpcSecGssOptions is null)
        {
            throw new InvalidOperationException("Expected the packed client package to expose RPCSEC_GSS settings on OpenNfsClientSettings.");
        }

        if (!string.Equals(settings.RpcSecGssOptions.TargetSpn, "nfs/sample.example.test@EXAMPLE.TEST", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the packed client package to preserve the configured Kerberos target SPN.");
        }

        if (!string.Equals(settings.RpcSecGssOptions.TargetName, "nfs/sample.example.test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the packed client package to derive the host-based Kerberos target name from the configured SPN.");
        }

        if (settings.RpcSecGssOptions.Service != OpenNfsRpcGssService.None)
        {
            throw new InvalidOperationException("Expected the packed client package to default RPCSEC_GSS service selection to auth-only mode.");
        }

        Console.WriteLine("CLIENT PACKAGE RPCSEC_GSS CONFIG OK");
        return 0;
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE RPCSEC_GSS CONFIG OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to report successful packaged RPCSEC_GSS configuration."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            string programSource = ReadmeSnippetSupport.ExtractClientExampleSnippet();

            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            _ = await DotnetCli.RunCheckedAsync(
                new[]
                {
                    "build",
                    "--disable-build-servers",
                    project.ProjectPath,
                    "-c",
                    "Release",
                    "--no-restore",
                },
                project.ProjectDirectory,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);
        }

        internal static void AssertEditorBrowsableState(MethodInfo? methodInfo, EditorBrowsableState expectedState, string displayName)
        {
            if (methodInfo is null)
            {
                throw new InvalidOperationException("Expected method '" + displayName + "' to exist on the current public client surface.");
            }

            EditorBrowsableAttribute? attribute = methodInfo.GetCustomAttribute<EditorBrowsableAttribute>();
            if (attribute is null || attribute.State != expectedState)
            {
                throw new InvalidOperationException(
                    "Expected method '"
                    + displayName
                    + "' to be marked with EditorBrowsableState."
                    + expectedState
                    + " on the current compatibility surface.");
            }
        }

        internal static async Task ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task listenerTask = RunListenerAsync(listener);

        try
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", listenerPort)
                .WithMountPort(listenerPort)
                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpOnly)
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                .Build();

            await client.ConnectAsync(CancellationToken.None);

            await ExpectExecutionFailureAsync(
                async () => { _ = await client.Exports.MountV3Async("/export", CancellationToken.None); },
                "MOUNT v3");
            await ExpectExecutionFailureAsync(
                async () => { _ = await client.Directories.GetRootV40Async(CancellationToken.None); },
                "NFSv4.0 root discovery");

            Console.WriteLine("CLIENT PACKAGE EXECUTION OK");
            return 0;
        }
        finally
        {
            listener.Stop();
            await listenerTask;
        }
    }

    internal static async Task ExpectExecutionFailureAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected " + operationName + " to fail against the disposable loopback listener.");
        }
        catch (Exception exception) when (string.Equals(exception.GetType().FullName, "OpenNFS.Client.OpenNfsClientIoException", StringComparison.Ordinal))
        {
            if (!exception.Message.Contains("failed", StringComparison.Ordinal)
                && !exception.Message.Contains("reset", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to reach the transport layer before failing, but received: "
                    + exception.Message,
                    exception);
            }
        }
        catch (Exception exception)
        {
            if (!string.Equals(exception.GetType().FullName, "OpenNFS.Client.OpenNfsClientProtocolException", StringComparison.Ordinal)
                && !exception.Message.Contains("RPC reply envelope", StringComparison.Ordinal)
                && !exception.Message.Contains("configured timeout", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to fail after entering the packaged execution path, but received "
                    + exception.GetType().FullName
                    + ": "
                    + exception.Message,
                    exception);
            }
        }
    }

    internal static async Task RunListenerAsync(TcpListener listener)
    {
        for (int index = 0; index < 2; index++)
        {
            try
            {
                using TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE EXECUTION OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to report successful packaged execution."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecutePackedClientPackageSupportsRawNfs42CompoundFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;
using OpenNFS.Client.Compound;
using OpenNFS.Client.Raw;
using OpenNFS.Protocol.V42.Generated;
using OpenNFS.Rpc.Generated;
using OpenNFS.Rpc.RpcMessages;
using OpenNFS.Rpc.Transport;
using OpenNFS.Rpc.Xdr;

public static class Program
{
    public static async Task<int> Main()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task listenerTask = RunListenerAsync(listener);

        try
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", listenerPort)
                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpOnly)
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                .Build();

            await client.ConnectAsync(CancellationToken.None);

            OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                new OpenNfsCompoundRequest(
                    OpenNfsProtocolVersion.Nfs42,
                    "packaged-v42",
                    new OpenNfsCompoundOperation[]
                    {
                        new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_IO_ADVISE,
                            EncodePayload(
                                new IO_ADVISE4args
                                {
                                    iaa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                    iaa_offset = new offset4 { Value = 0 },
                                    iaa_count = new length4 { Value = 4096 },
                                    iaa_hints = new bitmap4 { Value = Array.Empty<uint>() },
                                }.WriteTo)),
                    }),
                OpenNfsOperationIdempotency.NonIdempotent,
                CancellationToken.None);

            COMPOUND4res compound = ReadCompound(reply);
            if (compound.status != nfsstat4.NFS4_OK
                || compound.resarray is null
                || compound.resarray.Length != 2
                || compound.resarray[0].opputrootfh?.status != nfsstat4.NFS4_OK
                || compound.resarray[1].opio_advise?.ior_status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected the packed OpenNFS.Client package to restore bundled NFSv4.2 types and execute a raw NFSv4.2 COMPOUND round-trip.");
            }

            Console.WriteLine("CLIENT PACKAGE V42 OK");
            return 0;
        }
        finally
        {
            listener.Stop();
            await listenerTask;
        }
    }

    internal static COMPOUND4res ReadCompound(OpenNfsCompoundReply reply)
    {
        XdrReader reader = new XdrReader(reply.ReadAcceptedSuccessProcedurePayload());
        COMPOUND4res compound = COMPOUND4res.ReadFrom(reader);
        reader.EnsureFullyConsumed();
        return compound;
    }

    internal static byte[] EncodePayload(Action<XdrWriter> writePayload)
    {
        XdrWriter writer = new XdrWriter();
        writePayload(writer);
        return writer.ToArray();
    }

    internal static async Task RunListenerAsync(TcpListener listener)
    {
        try
        {
            using TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = tcpClient.GetStream();
            RpcTcpTransport transport = new RpcTcpTransport(stream);

            RpcMessageEnvelope requestEnvelope = await transport.ReceiveAsync(CancellationToken.None);
            call_body? callBody = requestEnvelope.Header.body?.cbody;
            if (callBody is null
                || callBody.prog != (uint)NFS4_PROGRAM_Program.Program
                || callBody.vers != (uint)NFS4_PROGRAM_Program.Version_NFS_V4
                || callBody.proc != (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND)
            {
                throw new InvalidOperationException("Expected the packaged client to emit a real NFSv4 COMPOUND RPC envelope.");
            }

            XdrReader requestReader = new XdrReader(requestEnvelope.ProcedurePayload);
            COMPOUND4args compoundArgs = COMPOUND4args.ReadFrom(requestReader);
            requestReader.EnsureFullyConsumed();

            if (compoundArgs.minorversion != 2
                || compoundArgs.argarray is null
                || compoundArgs.argarray.Length != 2
                || compoundArgs.argarray[0].argop != nfs_opnum4.OP_PUTROOTFH
                || compoundArgs.argarray[1].argop != nfs_opnum4.OP_IO_ADVISE)
            {
                throw new InvalidOperationException("Expected the packaged client to preserve minor version 2 and the requested NFSv4.2 operation ordering.");
            }

            COMPOUND4res compoundReply = new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = compoundArgs.tag,
                resarray = new[]
                {
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_PUTROOTFH,
                        opputrootfh = new PUTROOTFH4res
                        {
                            status = nfsstat4.NFS4_OK,
                        },
                    },
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_IO_ADVISE,
                        opio_advise = new IO_ADVISE4res
                        {
                            ior_status = nfsstat4.NFS4_OK,
                            resok4 = new IO_ADVISE4resok
                            {
                                ior_hints = new bitmap4 { Value = Array.Empty<uint>() },
                            },
                        },
                    },
                },
            };

            XdrWriter responseWriter = new XdrWriter();
            compoundReply.WriteTo(responseWriter);
            RpcMessageEnvelope replyEnvelope = RpcMessageFactory.CreateAcceptedReply(
                requestEnvelope.Header.xid,
                accept_stat.SUCCESS,
                RpcAuthenticationCodec.CreateNone(),
                responseWriter.ToArray());
            await transport.SendAsync(replyEnvelope, CancellationToken.None);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE V42 OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to restore bundled NFSv4.2 types and report successful raw v4.2 execution."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecutePackedClientPackageSupportsGroupedNfs42PlanningFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;
using OpenNFS.Protocol.V42.Generated;

public static class Program
{
    public static async Task<int> Main()
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer("127.0.0.1", 2049)
            .Build();

        await client.ConnectAsync(CancellationToken.None);

        byte[] fileHandle = new byte[] { 1, 2, 3, 4 };
        byte[] destinationFileHandle = new byte[] { 5, 6, 7, 8 };

        await VerifyPlanAsync(
            await client.Files.PrepareIoAdviseV42Async(
                fileHandle,
                0UL,
                4096UL,
                new[] { OpenNfsV42IoAdviceHint.Sequential, OpenNfsV42IoAdviceHint.WillNeed },
                CancellationToken.None),
            (uint)nfs_opnum4.OP_IO_ADVISE,
            "IO_ADVISE");

        await VerifyPlanAsync(
            await client.Files.PrepareReadPlusV42Async(
                fileHandle,
                0UL,
                1024U,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_READ_PLUS,
            "READ_PLUS");

        await VerifyPlanAsync(
            await client.Files.PrepareSeekV42Async(
                fileHandle,
                0UL,
                OpenNfsV42SeekTarget.Data,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_SEEK,
            "SEEK");

        await VerifyPlanAsync(
            await client.Files.PrepareAllocateV42Async(
                fileHandle,
                0UL,
                64UL,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_ALLOCATE,
            "ALLOCATE");

        await VerifyPlanAsync(
            await client.Files.PrepareDeallocateV42Async(
                fileHandle,
                0UL,
                64UL,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_DEALLOCATE,
            "DEALLOCATE");

        await VerifySavedHandlePlanAsync(
            await client.Files.PrepareCopyV42Async(
                fileHandle,
                destinationFileHandle,
                1UL,
                3UL,
                5UL,
                consecutive: true,
                synchronous: true,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_COPY,
            "COPY");

        await VerifySavedHandlePlanAsync(
            await client.Files.PrepareCloneV42Async(
                fileHandle,
                destinationFileHandle,
                2UL,
                4UL,
                6UL,
                CancellationToken.None),
            (uint)nfs_opnum4.OP_CLONE,
            "CLONE");

        Console.WriteLine("CLIENT PACKAGE GROUPED V42 OK");
        return 0;
    }

    internal static Task VerifyPlanAsync(OpenNfs.Client.Compound.OpenNfsCompoundPlan plan, uint expectedOperationNumber, string operationName)
    {
        if (plan.ProtocolVersion != OpenNfsProtocolVersion.Nfs42
            || plan.MinorVersion != 2U
            || plan.Operations.Count != 2
            || plan.Operations[0].OperationNumber != (uint)nfs_opnum4.OP_PUTFH
            || plan.Operations[1].OperationNumber != expectedOperationNumber)
        {
            throw new InvalidOperationException("Expected grouped " + operationName + " planning to preserve a minor-version-2 PUTFH + operation COMPOUND shape.");
        }

        return Task.CompletedTask;
    }

    internal static Task VerifySavedHandlePlanAsync(OpenNfs.Client.Compound.OpenNfsCompoundPlan plan, uint expectedOperationNumber, string operationName)
    {
        if (plan.ProtocolVersion != OpenNfsProtocolVersion.Nfs42
            || plan.MinorVersion != 2U
            || plan.Operations.Count != 4
            || plan.Operations[0].OperationNumber != (uint)nfs_opnum4.OP_PUTFH
            || plan.Operations[1].OperationNumber != (uint)nfs_opnum4.OP_SAVEFH
            || plan.Operations[2].OperationNumber != (uint)nfs_opnum4.OP_PUTFH
            || plan.Operations[3].OperationNumber != expectedOperationNumber)
        {
            throw new InvalidOperationException("Expected grouped " + operationName + " planning to preserve a minor-version-2 PUTFH + SAVEFH + PUTFH + operation COMPOUND shape.");
        }

        return Task.CompletedTask;
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE GROUPED V42 OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to restore grouped NFSv4.2 file helpers and report successful planning."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecutePackedClientPackageReusesGroupedNfs42SessionFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;
using OpenNFS.Protocol.V42.Generated;
using OpenNFS.Rpc.Generated;
using OpenNFS.Rpc.RpcMessages;
using OpenNFS.Rpc.Transport;
using OpenNFS.Rpc.Xdr;

public static class Program
{
    public static async Task<int> Main()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task listenerTask = RunListenerAsync(listener);

        try
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", listenerPort)
                .Build();
            await client.ConnectAsync(CancellationToken.None);

            byte[] fileHandle = new byte[] { 1, 2, 3, 4 };

            OpenNfsV42IoAdviseResult ioAdvise = await client.Files.IoAdviseV42Async(
                fileHandle,
                0UL,
                4096UL,
                new[] { OpenNfsV42IoAdviceHint.Sequential },
                CancellationToken.None);
            if (!ioAdvise.IsSuccess || ioAdvise.AcknowledgedHints.Count != 0)
            {
                throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 IO_ADVISE helper to succeed on the first sequenced call.");
            }

            OpenNfsV42SeekResult seek = await client.Files.SeekV42Async(
                fileHandle,
                64UL,
                OpenNfsV42SeekTarget.Hole,
                CancellationToken.None);
            if (!seek.IsSuccess || seek.Offset != 128UL || seek.EndOfFile)
            {
                throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 SEEK helper to reuse the established session and advance SEQUENCE on the second call.");
            }

            Console.WriteLine("CLIENT PACKAGE GROUPED V42 SESSION OK");
            return 0;
        }
        finally
        {
            listener.Stop();
            await listenerTask;
        }
    }

    internal static COMPOUND4res BuildCreateSessionReply(utf8str_cs? tag, byte[] sessionIdBytes)
    {
        channel_attrs4 channelAttributes = new channel_attrs4
        {
            ca_headerpadsize = new count4 { Value = 0U },
            ca_maxrequestsize = new count4 { Value = 1024U * 1024U },
            ca_maxresponsesize = new count4 { Value = 1024U * 1024U },
            ca_maxresponsesize_cached = new count4 { Value = 64U * 1024U },
            ca_maxoperations = new count4 { Value = 16U },
            ca_maxrequests = new count4 { Value = 4U },
            ca_rdma_ird = Array.Empty<uint>(),
        };

        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CREATE_SESSION,
                    opcreate_session = new CREATE_SESSION4res
                    {
                        csr_status = nfsstat4.NFS4_OK,
                        csr_resok4 = new CREATE_SESSION4resok
                        {
                            csr_sessionid = new sessionid4 { Value = sessionIdBytes },
                            csr_sequence = new sequenceid4 { Value = 1U },
                            csr_flags = 0U,
                            csr_fore_chan_attrs = channelAttributes,
                            csr_back_chan_attrs = channelAttributes,
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildExchangeIdReply(utf8str_cs? tag, ulong clientId)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_EXCHANGE_ID,
                    opexchange_id = new EXCHANGE_ID4res
                    {
                        eir_status = nfsstat4.NFS4_OK,
                        eir_resok4 = new EXCHANGE_ID4resok
                        {
                            eir_clientid = new clientid4 { Value = clientId },
                            eir_sequenceid = new sequenceid4 { Value = 1U },
                            eir_flags = 0U,
                            eir_state_protect = new state_protect4_r { spr_how = state_protect_how4.SP4_NONE },
                            eir_server_owner = new server_owner4
                            {
                                so_minor_id = 7UL,
                                so_major_id = new byte[] { 0xAA, 0xBB, 0xCC },
                            },
                            eir_server_scope = new byte[] { 0x01, 0x02 },
                            eir_server_impl_id = Array.Empty<nfs_impl_id4>(),
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildIoAdviseReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                BuildSequenceResult(sessionIdBytes, sequenceId),
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                },
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_IO_ADVISE,
                    opio_advise = new IO_ADVISE4res
                    {
                        ior_status = nfsstat4.NFS4_OK,
                        resok4 = new IO_ADVISE4resok
                        {
                            ior_hints = new bitmap4 { Value = Array.Empty<uint>() },
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildSeekReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId, ulong offset)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                BuildSequenceResult(sessionIdBytes, sequenceId),
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                },
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SEEK,
                    opseek = new SEEK4res
                    {
                        sa_status = nfsstat4.NFS4_OK,
                        resok4 = new seek_res4
                        {
                            sr_offset = new offset4 { Value = offset },
                            sr_eof = false,
                        },
                    },
                },
            },
        };
    }

    internal static nfs_resop4 BuildSequenceResult(byte[] sessionIdBytes, uint sequenceId)
    {
        return new nfs_resop4
        {
            resop = nfs_opnum4.OP_SEQUENCE,
            opsequence = new SEQUENCE4res
            {
                sr_status = nfsstat4.NFS4_OK,
                sr_resok4 = new SEQUENCE4resok
                {
                    sr_sessionid = new sessionid4 { Value = sessionIdBytes },
                    sr_sequenceid = new sequenceid4 { Value = sequenceId },
                    sr_slotid = new slotid4 { Value = 0U },
                    sr_highest_slotid = new slotid4 { Value = 3U },
                    sr_target_highest_slotid = new slotid4 { Value = 3U },
                    sr_status_flags = 0U,
                },
            },
        };
    }

    internal static async Task<ReceivedV42CompoundRequest> ReceiveV42CompoundRequestAsync(
        RpcTcpTransport transport,
        CancellationToken cancellationToken)
    {
        RpcMessageEnvelope envelope = await transport.ReceiveAsync(cancellationToken);
        XdrReader reader = new XdrReader(envelope.ProcedurePayload);
        COMPOUND4args arguments = COMPOUND4args.ReadFrom(reader);
        reader.EnsureFullyConsumed();
        return new ReceivedV42CompoundRequest(envelope.Header.xid, arguments);
    }

    internal static async Task RunListenerAsync(TcpListener listener)
    {
        const ulong clientId = 0x1234UL;
        byte[] sessionIdBytes = new byte[]
        {
            0x10, 0x11, 0x12, 0x13,
            0x20, 0x21, 0x22, 0x23,
            0x30, 0x31, 0x32, 0x33,
            0x40, 0x41, 0x42, 0x43,
        };

        try
        {
            using TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = tcpClient.GetStream();
            RpcTcpTransport transport = new RpcTcpTransport(stream);

            ReceivedV42CompoundRequest exchangeId = await ReceiveV42CompoundRequestAsync(transport, CancellationToken.None);
            ValidateExchangeIdRequest(exchangeId);
            await SendV42CompoundReplyAsync(transport, exchangeId.Xid, BuildExchangeIdReply(exchangeId.Arguments.tag, clientId), CancellationToken.None);

            ReceivedV42CompoundRequest createSession = await ReceiveV42CompoundRequestAsync(transport, CancellationToken.None);
            ValidateCreateSessionRequest(createSession, clientId);
            await SendV42CompoundReplyAsync(transport, createSession.Xid, BuildCreateSessionReply(createSession.Arguments.tag, sessionIdBytes), CancellationToken.None);

            ReceivedV42CompoundRequest ioAdvise = await ReceiveV42CompoundRequestAsync(transport, CancellationToken.None);
            ValidateIoAdviseRequest(ioAdvise, sessionIdBytes);
            await SendV42CompoundReplyAsync(transport, ioAdvise.Xid, BuildIoAdviseReply(ioAdvise.Arguments.tag, sessionIdBytes, 1U), CancellationToken.None);

            ReceivedV42CompoundRequest seek = await ReceiveV42CompoundRequestAsync(transport, CancellationToken.None);
            ValidateSeekRequest(seek, sessionIdBytes);
            await SendV42CompoundReplyAsync(transport, seek.Xid, BuildSeekReply(seek.Arguments.tag, sessionIdBytes, 2U, 128UL), CancellationToken.None);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal static async Task SendV42CompoundReplyAsync(
        RpcTcpTransport transport,
        uint xid,
        COMPOUND4res compoundReply,
        CancellationToken cancellationToken)
    {
        XdrWriter responseWriter = new XdrWriter();
        compoundReply.WriteTo(responseWriter);
        RpcMessageEnvelope replyEnvelope = RpcMessageFactory.CreateAcceptedReply(
            xid,
            accept_stat.SUCCESS,
            RpcAuthenticationCodec.CreateNone(),
            responseWriter.ToArray());
        await transport.SendAsync(replyEnvelope, cancellationToken);
    }

    internal static void ValidateCreateSessionRequest(ReceivedV42CompoundRequest request, ulong expectedClientId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 1
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_CREATE_SESSION
            || request.Arguments.argarray[0].opcreate_session?.csa_clientid?.Value != expectedClientId
            || request.Arguments.argarray[0].opcreate_session?.csa_sequence?.Value != 1U)
        {
            throw new InvalidOperationException("Expected the packaged reusable grouped NFSv4.2 session path to issue a single CREATE_SESSION request immediately after EXCHANGE_ID.");
        }
    }

    internal static void ValidateExchangeIdRequest(ReceivedV42CompoundRequest request)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 1
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_EXCHANGE_ID)
        {
            throw new InvalidOperationException("Expected the packaged reusable grouped NFSv4.2 session path to begin with a single EXCHANGE_ID request.");
        }
    }

    internal static void ValidateIoAdviseRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 3
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
            || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
            || request.Arguments.argarray[2].argop != nfs_opnum4.OP_IO_ADVISE
            || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 1U
            || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
            || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
        {
            throw new InvalidOperationException("Expected the packaged first grouped NFSv4.2 helper call to send SEQUENCE(1) + PUTFH + IO_ADVISE on the established session.");
        }
    }

    internal static void ValidateSeekRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 3
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
            || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
            || request.Arguments.argarray[2].argop != nfs_opnum4.OP_SEEK
            || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 2U
            || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
            || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
        {
            throw new InvalidOperationException("Expected the packaged second grouped NFSv4.2 helper call to reuse the same session and advance SEQUENCE to 2.");
        }
    }

    internal sealed class ReceivedV42CompoundRequest
    {
        internal ReceivedV42CompoundRequest(uint xid, COMPOUND4args arguments)
        {
            Xid = xid;
            Arguments = arguments;
        }

        internal uint Xid { get; }

        internal COMPOUND4args Arguments { get; }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE GROUPED V42 SESSION OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to restore the reusable grouped NFSv4.2 session path and report successful execution."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecutePackedClientPackageReconnectsGroupedNfs42SessionAfterTransportBreakFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;
using OpenNFS.Protocol.V42.Generated;
using OpenNFS.Rpc.Generated;
using OpenNFS.Rpc.RpcMessages;
using OpenNFS.Rpc.Transport;
using OpenNFS.Rpc.Xdr;

public static class Program
{
    public static async Task<int> Main()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task listenerTask = RunListenerAsync(listener);

        try
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", listenerPort)
                .Build();
            await client.ConnectAsync(CancellationToken.None);

            byte[] fileHandle = new byte[] { 1, 2, 3, 4 };

            OpenNfsV42IoAdviseResult ioAdvise = await client.Files.IoAdviseV42Async(
                fileHandle,
                0UL,
                4096UL,
                new[] { OpenNfsV42IoAdviceHint.Sequential },
                CancellationToken.None);
            if (!ioAdvise.IsSuccess || ioAdvise.AcknowledgedHints.Count != 0)
            {
                throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 reconnect path to succeed on the first sequenced call before the transport break.");
            }

            OpenNfsV42SeekResult seek = await client.Files.SeekV42Async(
                fileHandle,
                64UL,
                OpenNfsV42SeekTarget.Hole,
                CancellationToken.None);
            if (!seek.IsSuccess || seek.Offset != 128UL || seek.EndOfFile)
            {
                throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 reconnect path to bind the replacement connection and preserve SEQUENCE on the replayed second call.");
            }

            Console.WriteLine("CLIENT PACKAGE GROUPED V42 RECONNECT OK");
            return 0;
        }
        finally
        {
            listener.Stop();
            await listenerTask;
        }
    }

    internal static COMPOUND4res BuildBindConnectionReply(utf8str_cs? tag, byte[] sessionIdBytes)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                    opbind_conn_to_session = new BIND_CONN_TO_SESSION4res
                    {
                        bctsr_status = nfsstat4.NFS4_OK,
                        bctsr_resok4 = new BIND_CONN_TO_SESSION4resok
                        {
                            bctsr_sessid = new sessionid4
                            {
                                Value = sessionIdBytes,
                            },
                            bctsr_dir = channel_dir_from_server4.CDFS4_FORE,
                            bctsr_use_conn_in_rdma_mode = false,
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildCreateSessionReply(utf8str_cs? tag, byte[] sessionIdBytes)
    {
        channel_attrs4 channelAttributes = new channel_attrs4
        {
            ca_headerpadsize = new count4 { Value = 0U },
            ca_maxrequestsize = new count4 { Value = 1024U * 1024U },
            ca_maxresponsesize = new count4 { Value = 1024U * 1024U },
            ca_maxresponsesize_cached = new count4 { Value = 64U * 1024U },
            ca_maxoperations = new count4 { Value = 16U },
            ca_maxrequests = new count4 { Value = 4U },
            ca_rdma_ird = Array.Empty<uint>(),
        };

        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CREATE_SESSION,
                    opcreate_session = new CREATE_SESSION4res
                    {
                        csr_status = nfsstat4.NFS4_OK,
                        csr_resok4 = new CREATE_SESSION4resok
                        {
                            csr_sessionid = new sessionid4 { Value = sessionIdBytes },
                            csr_sequence = new sequenceid4 { Value = 1U },
                            csr_flags = 0U,
                            csr_fore_chan_attrs = channelAttributes,
                            csr_back_chan_attrs = channelAttributes,
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildExchangeIdReply(utf8str_cs? tag, ulong clientId)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_EXCHANGE_ID,
                    opexchange_id = new EXCHANGE_ID4res
                    {
                        eir_status = nfsstat4.NFS4_OK,
                        eir_resok4 = new EXCHANGE_ID4resok
                        {
                            eir_clientid = new clientid4 { Value = clientId },
                            eir_sequenceid = new sequenceid4 { Value = 1U },
                            eir_flags = 0U,
                            eir_state_protect = new state_protect4_r { spr_how = state_protect_how4.SP4_NONE },
                            eir_server_owner = new server_owner4
                            {
                                so_minor_id = 7UL,
                                so_major_id = new byte[] { 0xAA, 0xBB, 0xCC },
                            },
                            eir_server_scope = new byte[] { 0x01, 0x02 },
                            eir_server_impl_id = Array.Empty<nfs_impl_id4>(),
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildIoAdviseReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                BuildSequenceResult(sessionIdBytes, sequenceId),
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                },
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_IO_ADVISE,
                    opio_advise = new IO_ADVISE4res
                    {
                        ior_status = nfsstat4.NFS4_OK,
                        resok4 = new IO_ADVISE4resok
                        {
                            ior_hints = new bitmap4 { Value = Array.Empty<uint>() },
                        },
                    },
                },
            },
        };
    }

    internal static COMPOUND4res BuildSeekReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId, ulong offset)
    {
        return new COMPOUND4res
        {
            status = nfsstat4.NFS4_OK,
            tag = tag,
            resarray = new[]
            {
                BuildSequenceResult(sessionIdBytes, sequenceId),
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                },
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SEEK,
                    opseek = new SEEK4res
                    {
                        sa_status = nfsstat4.NFS4_OK,
                        resok4 = new seek_res4
                        {
                            sr_offset = new offset4 { Value = offset },
                            sr_eof = false,
                        },
                    },
                },
            },
        };
    }

    internal static nfs_resop4 BuildSequenceResult(byte[] sessionIdBytes, uint sequenceId)
    {
        return new nfs_resop4
        {
            resop = nfs_opnum4.OP_SEQUENCE,
            opsequence = new SEQUENCE4res
            {
                sr_status = nfsstat4.NFS4_OK,
                sr_resok4 = new SEQUENCE4resok
                {
                    sr_sessionid = new sessionid4 { Value = sessionIdBytes },
                    sr_sequenceid = new sequenceid4 { Value = sequenceId },
                    sr_slotid = new slotid4 { Value = 0U },
                    sr_highest_slotid = new slotid4 { Value = 3U },
                    sr_target_highest_slotid = new slotid4 { Value = 3U },
                    sr_status_flags = 0U,
                },
            },
        };
    }

    internal static async Task<ReceivedV42CompoundRequest> ReceiveV42CompoundRequestAsync(
        RpcTcpTransport transport,
        CancellationToken cancellationToken)
    {
        RpcMessageEnvelope envelope = await transport.ReceiveAsync(cancellationToken);
        XdrReader reader = new XdrReader(envelope.ProcedurePayload);
        COMPOUND4args arguments = COMPOUND4args.ReadFrom(reader);
        reader.EnsureFullyConsumed();
        return new ReceivedV42CompoundRequest(envelope.Header.xid, arguments);
    }

    internal static async Task RunListenerAsync(TcpListener listener)
    {
        const ulong clientId = 0x1234UL;
        byte[] sessionIdBytes = new byte[]
        {
            0x50, 0x51, 0x52, 0x53,
            0x60, 0x61, 0x62, 0x63,
            0x70, 0x71, 0x72, 0x73,
            0x80, 0x81, 0x82, 0x83,
        };

        try
        {
            using (TcpClient firstClient = await listener.AcceptTcpClientAsync())
            using (NetworkStream firstStream = firstClient.GetStream())
            {
                RpcTcpTransport firstTransport = new RpcTcpTransport(firstStream);

                ReceivedV42CompoundRequest exchangeId = await ReceiveV42CompoundRequestAsync(firstTransport, CancellationToken.None);
                ValidateExchangeIdRequest(exchangeId);
                await SendV42CompoundReplyAsync(firstTransport, exchangeId.Xid, BuildExchangeIdReply(exchangeId.Arguments.tag, clientId), CancellationToken.None);

                ReceivedV42CompoundRequest createSession = await ReceiveV42CompoundRequestAsync(firstTransport, CancellationToken.None);
                ValidateCreateSessionRequest(createSession, clientId);
                await SendV42CompoundReplyAsync(firstTransport, createSession.Xid, BuildCreateSessionReply(createSession.Arguments.tag, sessionIdBytes), CancellationToken.None);

                ReceivedV42CompoundRequest ioAdvise = await ReceiveV42CompoundRequestAsync(firstTransport, CancellationToken.None);
                ValidateIoAdviseRequest(ioAdvise, sessionIdBytes);
                await SendV42CompoundReplyAsync(firstTransport, ioAdvise.Xid, BuildIoAdviseReply(ioAdvise.Arguments.tag, sessionIdBytes, 1U), CancellationToken.None);
            }

            using TcpClient secondClient = await listener.AcceptTcpClientAsync();
            using NetworkStream secondStream = secondClient.GetStream();
            RpcTcpTransport secondTransport = new RpcTcpTransport(secondStream);

            ReceivedV42CompoundRequest bindConnection = await ReceiveV42CompoundRequestAsync(secondTransport, CancellationToken.None);
            ValidateBindConnectionRequest(bindConnection, sessionIdBytes);
            await SendV42CompoundReplyAsync(secondTransport, bindConnection.Xid, BuildBindConnectionReply(bindConnection.Arguments.tag, sessionIdBytes), CancellationToken.None);

            ReceivedV42CompoundRequest seek = await ReceiveV42CompoundRequestAsync(secondTransport, CancellationToken.None);
            ValidateSeekRequest(seek, sessionIdBytes);
            await SendV42CompoundReplyAsync(secondTransport, seek.Xid, BuildSeekReply(seek.Arguments.tag, sessionIdBytes, 2U, 128UL), CancellationToken.None);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal static async Task SendV42CompoundReplyAsync(
        RpcTcpTransport transport,
        uint xid,
        COMPOUND4res compoundReply,
        CancellationToken cancellationToken)
    {
        XdrWriter responseWriter = new XdrWriter();
        compoundReply.WriteTo(responseWriter);
        RpcMessageEnvelope replyEnvelope = RpcMessageFactory.CreateAcceptedReply(
            xid,
            accept_stat.SUCCESS,
            RpcAuthenticationCodec.CreateNone(),
            responseWriter.ToArray());
        await transport.SendAsync(replyEnvelope, cancellationToken);
    }

    internal static void ValidateBindConnectionRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 1
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_BIND_CONN_TO_SESSION
            || request.Arguments.argarray[0].opbind_conn_to_session?.bctsa_sessid?.Value is null
            || !request.Arguments.argarray[0].opbind_conn_to_session!.bctsa_sessid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
        {
            throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 reconnect path to send a single BIND_CONN_TO_SESSION request for the established session after the transport break.");
        }
    }

    internal static void ValidateCreateSessionRequest(ReceivedV42CompoundRequest request, ulong expectedClientId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 1
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_CREATE_SESSION
            || request.Arguments.argarray[0].opcreate_session?.csa_clientid?.Value != expectedClientId
            || request.Arguments.argarray[0].opcreate_session?.csa_sequence?.Value != 1U)
        {
            throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 reconnect path to issue a single CREATE_SESSION request immediately after EXCHANGE_ID.");
        }
    }

    internal static void ValidateExchangeIdRequest(ReceivedV42CompoundRequest request)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 1
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_EXCHANGE_ID)
        {
            throw new InvalidOperationException("Expected the packaged grouped NFSv4.2 reconnect path to begin with a single EXCHANGE_ID request.");
        }
    }

    internal static void ValidateIoAdviseRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 3
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
            || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
            || request.Arguments.argarray[2].argop != nfs_opnum4.OP_IO_ADVISE
            || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 1U
            || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
            || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
        {
            throw new InvalidOperationException("Expected the packaged first grouped NFSv4.2 helper call to send SEQUENCE(1) + PUTFH + IO_ADVISE on the established session.");
        }
    }

    internal static void ValidateSeekRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
    {
        if (request.Arguments.minorversion != 2U
            || request.Arguments.argarray is null
            || request.Arguments.argarray.Length != 3
            || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
            || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
            || request.Arguments.argarray[2].argop != nfs_opnum4.OP_SEEK
            || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 2U
            || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
            || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
        {
            throw new InvalidOperationException("Expected the packaged second grouped NFSv4.2 helper call to reconnect, bind the same session, and advance SEQUENCE to 2.");
        }
    }

    internal sealed class ReceivedV42CompoundRequest
    {
        internal ReceivedV42CompoundRequest(uint xid, COMPOUND4args arguments)
        {
            Xid = xid;
            Arguments = arguments;
        }

        internal uint Xid { get; }

        internal COMPOUND4args Arguments { get; }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE GROUPED V42 RECONNECT OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to restore the grouped NFSv4.2 reconnect-and-bind path and report successful execution."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static async Task ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer("127.0.0.1", 2049)
            .Build();

        await ExpectLifetimeFailureAsync(
            async () => { _ = await client.Exports.PrepareMountV3Async("/export", CancellationToken.None); },
            "MOUNT planning");
        await ExpectLifetimeFailureAsync(
            async () => { _ = await client.Directories.GetRootV40Async(CancellationToken.None); },
            "NFSv4.0 root discovery");

        Console.WriteLine("CLIENT PACKAGE NEGATIVE OK");
        return 0;
    }

    internal static async Task ExpectLifetimeFailureAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected " + operationName + " to reject usage before ConnectAsync.");
        }
        catch (InvalidOperationException exception)
        {
            if (!exception.Message.Contains("ConnectAsync", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the packaged client lifetime failure to direct consumers toward ConnectAsync/OpenAsync, but received: "
                    + exception.Message,
                    exception);
            }
        }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE NEGATIVE OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to report successful negative-path validation."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }
    }
}
