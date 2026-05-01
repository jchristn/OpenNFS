namespace Test.Shared.Infrastructure
{
    internal enum FaultInjectingRpcProxyMode
    {
        Transparent = 0,
        DropFirstRequestWithoutForwarding = 1,
        DuplicateFirstForwardedRequest = 2,
        DropFirstReplyAfterForwarding = 3,
    }
}
