namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Remote endpoint exposed by the public OpenNFS client surface.
    /// </summary>
    public sealed class OpenNfsEndpoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsEndpoint"/> class.
        /// </summary>
        /// <param name="host">Remote host name or IP address.</param>
        /// <param name="port">
        /// Remote listener port.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>65535</c>.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="host"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is outside the supported range.</exception>
        public OpenNfsEndpoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("The client endpoint host must contain a host name or IP address.", nameof(host));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "The client endpoint port must be between 1 and 65535.");
            }

            Host = host;
            Port = port;
        }

        /// <summary>
        /// Gets the remote host name or IP address.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets the remote listener port.
        /// </summary>
        public int Port { get; }
    }
}
