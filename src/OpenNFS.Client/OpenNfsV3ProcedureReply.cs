namespace OpenNFS.Client
{
    using System;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Full raw-reply result for a public NFSv3-era procedure execution.
    /// </summary>
    public sealed class OpenNfsV3ProcedureReply
    {
        private readonly byte[] _encodedReply;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV3ProcedureReply"/> class.
        /// </summary>
        /// <param name="plan">Validated procedure plan that produced the reply.</param>
        /// <param name="operationName">Diagnostic operation name used during execution.</param>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required reference input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operationName"/> is empty or whitespace.</exception>
        public OpenNfsV3ProcedureReply(
            OpenNfsV3ProcedurePlan plan,
            string operationName,
            byte[] encodedReply)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(encodedReply);

            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("The diagnostic operation name cannot be empty or whitespace.", nameof(operationName));
            }

            Plan = plan;
            OperationName = operationName;
            _encodedReply = encodedReply.AsSpan().ToArray();
        }

        /// <summary>
        /// Gets the validated procedure plan that produced this reply.
        /// </summary>
        public OpenNfsV3ProcedurePlan Plan { get; }

        /// <summary>
        /// Gets the diagnostic operation name associated with this reply.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the full encoded ONC RPC reply bytes.
        /// </summary>
        public ReadOnlyMemory<byte> EncodedReply
        {
            get
            {
                return _encodedReply;
            }
        }

        /// <summary>
        /// Reads the accepted-success procedure payload from the encoded reply.
        /// </summary>
        /// <returns>The raw accepted-success procedure payload bytes.</returns>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the reply is rejected, accepted with a non-success RPC status, or malformed.
        /// </exception>
        public byte[] ReadAcceptedSuccessProcedurePayload()
        {
            return OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(_encodedReply, OperationName).ToArray();
        }

        /// <summary>
        /// Validates that the reply succeeded and did not carry a procedure payload.
        /// </summary>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the reply is rejected, accepted with a non-success RPC status, or carries an unexpected payload.
        /// </exception>
        public void EnsureAcceptedSuccessWithoutPayload()
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(_encodedReply, OperationName);
        }
    }
}
