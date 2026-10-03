namespace OpenNFS.Rpc.Telemetry
{
    /// <summary>
    /// The bounded classification of one server RPC reply: its outcome, protocol status name, and error type.
    /// </summary>
    /// <remarks>Immutable and thread safe.</remarks>
    internal readonly struct OpenNfsRpcReplyClassification
    {
        internal OpenNfsRpcReplyClassification(string outcome, string status, string? errorType, bool authenticationRejected)
        {
            Outcome = outcome;
            Status = status;
            ErrorType = errorType;
            AuthenticationRejected = authenticationRejected;
        }

        internal string Outcome { get; }

        internal string Status { get; }

        internal string? ErrorType { get; }

        internal bool AuthenticationRejected { get; }
    }
}
