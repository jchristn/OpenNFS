namespace Test.Shared.Infrastructure
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Reserves a loopback ephemeral port that is bindable for both UDP and TCP simultaneously.
    /// </summary>
    /// <remarks>
    /// Test cases that exercise UDP fallback for NFSv3 traffic require the same port to be reachable
    /// via both transports so a single configured client endpoint can fall back from TCP to UDP. On
    /// Windows the OS may hand out an ephemeral UDP port that has been reserved exclusively for some
    /// other service (Hyper-V, WSL2, Docker Desktop, and other components reserve ranges visible via
    /// <c>netsh interface ipv4 show excludedportrange</c>); the subsequent <see cref="TcpListener"/>
    /// bind on the same port then fails with <c>WSAEACCES</c> (10013). Retry the dual-bind a bounded
    /// number of times so a hosted runner with a particular reservation table does not flake the
    /// suite. Returning the two bound objects together transfers ownership to the caller, which is
    /// responsible for disposing both.
    /// </remarks>
    internal static class LoopbackDualBindReservation
    {
        private const int MaxAttempts = 32;

        /// <summary>
        /// Reserves a loopback port that is bindable for both UDP and TCP.
        /// </summary>
        /// <returns>The bound <see cref="UdpClient"/>, the started <see cref="TcpListener"/>, and the
        /// shared port number. The caller must dispose both objects.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no loopback port could be reserved
        /// for both transports after <see cref="MaxAttempts"/> tries.</exception>
        internal static (UdpClient UdpServer, TcpListener TcpListener, int Port) Reserve()
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                UdpClient? udpClient = null;
                try
                {
                    udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                }
                catch (SocketException)
                {
                    udpClient?.Dispose();
                    continue;
                }

                int port = ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
                TcpListener listener = new TcpListener(IPAddress.Loopback, port);
                try
                {
                    listener.Start();
                    return (udpClient, listener, port);
                }
                catch (SocketException)
                {
                    listener.Stop();
                    udpClient.Dispose();
                    continue;
                }
            }

            throw new InvalidOperationException(
                "Could not reserve a loopback port bindable for both UDP and TCP after "
                + MaxAttempts
                + " attempts.");
        }
    }
}
