namespace OpenNFS.Client.Compound
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Public raw-client request for an NFSv4 COMPOUND payload.
    /// </summary>
    public sealed class OpenNfsCompoundRequest
    {
        private readonly OpenNfsCompoundOperation[] _Operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsCompoundRequest"/> class.
        /// </summary>
        /// <param name="protocolVersion">
        /// NFSv4 protocol version for the COMPOUND payload.
        /// Valid values: <see cref="OpenNfsProtocolVersion.Nfs40"/>, <see cref="OpenNfsProtocolVersion.Nfs41"/>, <see cref="OpenNfsProtocolVersion.Nfs42"/>.
        /// </param>
        /// <param name="tag">Client tag for the COMPOUND payload. Empty is allowed.</param>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="retryMode">
        /// Retry mode for the COMPOUND issue plan.
        /// Default value: <see cref="OpenNfsRetryMode.UseClientPolicy"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tag"/> or <paramref name="operations"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="protocolVersion"/> is not an NFSv4 version, <paramref name="operations"/> is empty, or <paramref name="operations"/> contains null entries.</exception>
        public OpenNfsCompoundRequest(
            OpenNfsProtocolVersion protocolVersion,
            string tag,
            IReadOnlyCollection<OpenNfsCompoundOperation> operations,
            OpenNfsRetryMode retryMode = OpenNfsRetryMode.UseClientPolicy)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(operations);

            if (protocolVersion == OpenNfsProtocolVersion.Nfs3)
            {
                throw new ArgumentException("The COMPOUND request surface supports only NFSv4 protocol versions.", nameof(protocolVersion));
            }

            if (operations.Count < 1)
            {
                throw new ArgumentException("The COMPOUND request must contain at least one operation.", nameof(operations));
            }

            _Operations = new OpenNfsCompoundOperation[operations.Count];
            int index = 0;

            foreach (OpenNfsCompoundOperation? operation in operations)
            {
                if (operation is null)
                {
                    throw new ArgumentException("The COMPOUND operation list cannot contain null entries.", nameof(operations));
                }

                _Operations[index++] = operation;
            }

            ProtocolVersion = protocolVersion;
            Tag = tag;
            RetryMode = retryMode;
        }

        /// <summary>
        /// Gets the NFSv4 protocol version for the COMPOUND payload.
        /// </summary>
        public OpenNfsProtocolVersion ProtocolVersion { get; }

        /// <summary>
        /// Gets the client tag for the COMPOUND payload.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Gets the ordered COMPOUND operations.
        /// </summary>
        public IReadOnlyList<OpenNfsCompoundOperation> Operations
        {
            get
            {
                return _Operations;
            }
        }

        /// <summary>
        /// Gets the retry mode for the COMPOUND issue plan.
        /// </summary>
        public OpenNfsRetryMode RetryMode { get; }
    }
}
