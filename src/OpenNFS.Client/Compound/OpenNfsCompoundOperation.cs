namespace OpenNFS.Client.Compound
{
    using System;

    /// <summary>
    /// Public raw-client representation of a single NFSv4 COMPOUND operation payload.
    /// </summary>
    public sealed class OpenNfsCompoundOperation
    {
        private readonly byte[] _OperationPayload;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsCompoundOperation"/> class.
        /// </summary>
        /// <param name="operationNumber">NFSv4 operation number.</param>
        /// <param name="operationPayload">XDR-encoded operation payload.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationPayload"/> is null.</exception>
        public OpenNfsCompoundOperation(uint operationNumber, byte[] operationPayload)
        {
            ArgumentNullException.ThrowIfNull(operationPayload);

            OperationNumber = operationNumber;
            _OperationPayload = operationPayload.AsSpan().ToArray();
        }

        /// <summary>
        /// Gets the NFSv4 operation number.
        /// </summary>
        public uint OperationNumber { get; }

        /// <summary>
        /// Gets the XDR-encoded operation payload.
        /// </summary>
        public ReadOnlyMemory<byte> OperationPayload
        {
            get
            {
                return _OperationPayload;
            }
        }
    }
}
