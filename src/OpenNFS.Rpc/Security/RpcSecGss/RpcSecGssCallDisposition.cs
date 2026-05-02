namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// The disposition of an RPCSEC_GSS-aware credential evaluation: either the dispatcher should
    /// continue to its own procedure dispatch (with optional authenticated identity attached) or it
    /// should send the supplied reply directly back to the caller without further processing.
    /// </summary>
    public sealed class RpcSecGssCallDisposition
    {
        private RpcSecGssCallDisposition(
            bool continueProcessing,
            RpcMessageEnvelope? reply,
            RpcSecGssAuthenticatedCall? authenticatedCall)
        {
            ContinueProcessing = continueProcessing;
            Reply = reply;
            AuthenticatedCall = authenticatedCall;
        }

        /// <summary>
        /// Gets a value indicating whether the dispatcher should continue to procedure dispatch.
        /// When <c>true</c>, <see cref="Reply"/> is null. When <c>false</c>, <see cref="Reply"/> is the
        /// reply the dispatcher must return as-is.
        /// </summary>
        public bool ContinueProcessing { get; }

        /// <summary>
        /// Gets the reply to send back to the caller when <see cref="ContinueProcessing"/> is <c>false</c>.
        /// </summary>
        public RpcMessageEnvelope? Reply { get; }

        /// <summary>
        /// Gets the authenticated call context when <see cref="ContinueProcessing"/> is <c>true</c>
        /// and the inbound credential was an RPCSEC_GSS DATA call. Null for non-RPCSEC_GSS calls.
        /// </summary>
        public RpcSecGssAuthenticatedCall? AuthenticatedCall { get; }

        /// <summary>
        /// Builds a "continue to dispatch" disposition with no authenticated identity attached. Used
        /// for non-RPCSEC_GSS credentials.
        /// </summary>
        /// <returns>The disposition.</returns>
        public static RpcSecGssCallDisposition Continue()
        {
            return new RpcSecGssCallDisposition(continueProcessing: true, reply: null, authenticatedCall: null);
        }

        /// <summary>
        /// Builds a "continue to dispatch" disposition with an authenticated RPCSEC_GSS DATA call.
        /// </summary>
        /// <param name="authenticatedCall">The authenticated call.</param>
        /// <returns>The disposition.</returns>
        public static RpcSecGssCallDisposition Continue(RpcSecGssAuthenticatedCall authenticatedCall)
        {
            ArgumentNullException.ThrowIfNull(authenticatedCall);
            return new RpcSecGssCallDisposition(continueProcessing: true, reply: null, authenticatedCall: authenticatedCall);
        }

        /// <summary>
        /// Builds a "send reply directly" disposition.
        /// </summary>
        /// <param name="replyEnvelope">The reply to send back to the caller.</param>
        /// <returns>The disposition.</returns>
        public static RpcSecGssCallDisposition WithReply(RpcMessageEnvelope replyEnvelope)
        {
            ArgumentNullException.ThrowIfNull(replyEnvelope);
            return new RpcSecGssCallDisposition(continueProcessing: false, reply: replyEnvelope, authenticatedCall: null);
        }
    }
}
