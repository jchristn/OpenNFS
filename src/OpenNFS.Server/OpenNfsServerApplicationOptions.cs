namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Configures the public runnable server-application surface.
    /// </summary>
    public sealed class OpenNfsServerApplicationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the NFSv3-era listener surface should be started.
        /// </summary>
        public bool EnableNfsV3 { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the NFSv4.0 listener surface should be started.
        /// </summary>
        public bool EnableNfs40 { get; set; } = true;

        /// <summary>
        /// Gets or sets the listener bind address.
        /// Default value: <c>0.0.0.0</c>.
        /// </summary>
        public string ListenerAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the MOUNT v3 TCP port.
        /// Use <c>0</c> for an ephemeral port.
        /// Default value: <c>20048</c>.
        /// </summary>
        public int MountPort { get; set; } = 20048;

        /// <summary>
        /// Gets or sets the NFSv3 TCP port.
        /// Use <c>0</c> for an ephemeral port.
        /// Default value: <c>2049</c>.
        /// </summary>
        public int NfsPort { get; set; } = 2049;

        /// <summary>
        /// Gets or sets the NLM v4 TCP port.
        /// Use <c>0</c> for an ephemeral port.
        /// Default value: <c>0</c>.
        /// </summary>
        public int NlmPort { get; set; }

        /// <summary>
        /// Gets or sets the NSM TCP port.
        /// Use <c>0</c> for an ephemeral port.
        /// Default value: <c>0</c>.
        /// </summary>
        public int NsmPort { get; set; }

        /// <summary>
        /// Gets or sets the NFSv4.0 TCP port.
        /// Use <c>0</c> for an ephemeral port.
        /// Default value: <c>3049</c>.
        /// </summary>
        public int Nfs40Port { get; set; } = 3049;

        internal OpenNfsServerApplicationOptions Clone()
        {
            return new OpenNfsServerApplicationOptions
            {
                EnableNfsV3 = EnableNfsV3,
                EnableNfs40 = EnableNfs40,
                ListenerAddress = ListenerAddress,
                MountPort = MountPort,
                NfsPort = NfsPort,
                NlmPort = NlmPort,
                NsmPort = NsmPort,
                Nfs40Port = Nfs40Port,
            };
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(ListenerAddress))
            {
                throw new ArgumentException("The listener address must contain a non-empty value.", nameof(ListenerAddress));
            }

            if (!EnableNfsV3 && !EnableNfs40)
            {
                throw new InvalidOperationException("At least one server protocol surface must be enabled.");
            }

            ValidatePort(MountPort, nameof(MountPort));
            ValidatePort(NfsPort, nameof(NfsPort));
            ValidatePort(NlmPort, nameof(NlmPort));
            ValidatePort(NsmPort, nameof(NsmPort));
            ValidatePort(Nfs40Port, nameof(Nfs40Port));
        }

        private static void ValidatePort(int port, string propertyName)
        {
            if (port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(propertyName, port, "The configured port must be between 0 and 65535.");
            }
        }
    }
}
