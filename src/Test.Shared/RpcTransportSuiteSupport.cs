namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RecordMarking;

    /// <summary>
    /// Shared helpers for the RPC transport suite catalog.
    /// </summary>
    internal static class RpcTransportSuiteSupport
    {
        internal static async Task ExpectTimeoutAsync(Func<Task> action, string expectedMessageFragment)
        {
            try
            {
                await action().ConfigureAwait(false);
                throw new InvalidOperationException("Expected the transport operation to time out.");
            }
            catch (TimeoutException exception)
            {
                if (!exception.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected a timeout message containing '" + expectedMessageFragment + "', but received '" + exception.Message + "'.");
                }
            }
        }

        internal static IReadOnlyList<RecordMarkingFragmentHeader> ReadFragmentHeaders(byte[] recordMarkedMessage)
        {
            List<RecordMarkingFragmentHeader> fragmentHeaders = new List<RecordMarkingFragmentHeader>();
            int offset = 0;

            while (offset < recordMarkedMessage.Length)
            {
                RecordMarkingFragmentHeader header = RecordMarkingCodec.ReadHeader(recordMarkedMessage.AsSpan(offset));
                fragmentHeaders.Add(header);
                offset += RecordMarkingCodec.HeaderLength + header.FragmentLength;
            }

            return fragmentHeaders;
        }
    }
}
