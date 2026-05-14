namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedMountTransportCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisExecuteOverLoopbackTcp",
                    displayName: "Grouped export APIs execute against the default TCP RPC executor",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                        listener.Start();

                        try
                        {
                            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                            Task serverTask = Task.Run(
                                async () =>
                                {
                                    using TcpClient acceptedClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                                    using NetworkStream stream = acceptedClient.GetStream();
                                    RpcTcpTransport transport = new RpcTcpTransport(
                                        stream,
                                        new RpcTransportOptions(
                                            timeouts: new RpcTransportTimeouts(
                                                readTimeout: TimeSpan.FromSeconds(5),
                                                writeTimeout: TimeSpan.FromSeconds(5))));

                                    RpcMessageEnvelope request = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                    RpcGenerated.call_body? callBody = request.Header.body?.cbody;
                                    if (callBody is null
                                        || callBody.prog != 100005
                                        || callBody.vers != 3
                                        || callBody.proc != 5
                                        || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                                    {
                                        throw new InvalidOperationException("Expected the default grouped export execution path to issue a TCP MOUNT v3 EXPORT call.");
                                    }

                                    await transport.SendAsync(
                                        CreateAcceptedReplyEnvelope(
                                            request.Header.xid,
                                            new exports
                                            {
                                                Value = new exportnode
                                                {
                                                    ex_dir = new dirpath
                                                    {
                                                        Value = "/srv/share",
                                                    },
                                                    ex_groups = new groups(),
                                                    ex_next = new exports(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer)),
                                        cancellationToken).ConfigureAwait(false);
                                },
                                cancellationToken);

                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);

                            if (exports.Count != 1
                                || !string.Equals(exports[0].ExportPath, "/srv/share", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the default TCP grouped export execution path to decode the loopback server reply.");
                            }

                            await serverTask.ConfigureAwait(false);
                        }
                        finally
                        {
                            listener.Stop();
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisFallBackToUdpForV3Policy",
                    displayName: "Grouped export APIs fall back to UDP for v3-era transport policy when TCP fails",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        LoopbackDualBindReservation.Reservation reservation =
                            LoopbackDualBindReservation.Reserve();
                        using UdpClient udpServer = reservation.UdpServer;
                        TcpListener tcpFailureListener = reservation.TcpListener;
                        int port = reservation.Port;

                        Task serverTask = Task.Run(
                            async () =>
                            {
                                UdpReceiveResult receivedDatagram = await udpServer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                RpcMessageEnvelope request = RpcMessageCodec.Decode(receivedDatagram.Buffer);
                                RpcGenerated.call_body? callBody = request.Header.body?.cbody;
                                if (callBody is null
                                    || callBody.prog != 100005
                                    || callBody.vers != 3
                                    || callBody.proc != 5
                                    || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                                {
                                    throw new InvalidOperationException("Expected UDP fallback to preserve the MOUNT v3 EXPORT call envelope.");
                                }

                                byte[] replyBytes = RpcMessageCodec.Encode(
                                    CreateAcceptedReplyEnvelope(
                                        request.Header.xid,
                                        new exports
                                        {
                                            Value = new exportnode
                                            {
                                                ex_dir = new dirpath
                                                {
                                                    Value = "/srv/udp",
                                                },
                                                ex_groups = new groups(),
                                                ex_next = new exports(),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer)));

                                _ = await udpServer.SendAsync(
                                    replyBytes,
                                    replyBytes.Length,
                                    receivedDatagram.RemoteEndPoint).ConfigureAwait(false);
                            },
                            cancellationToken);
                        Task tcpFailureTask = Task.Run(
                            async () =>
                            {
                                using TcpClient failedClient = await tcpFailureListener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                            },
                            cancellationToken);

                        try
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithConnectionTimeout(TimeSpan.FromMilliseconds(200))
                                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);

                            if (exports.Count != 1
                                || !string.Equals(exports[0].ExportPath, "/srv/udp", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected grouped client execution to decode the UDP fallback reply.");
                            }

                            await serverTask.ConfigureAwait(false);
                            await tcpFailureTask.ConfigureAwait(false);
                        }
                        finally
                        {
                            tcpFailureListener.Stop();
                        }
                    }),
            };
        }
    }
}
