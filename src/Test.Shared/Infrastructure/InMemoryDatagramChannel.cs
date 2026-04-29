namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Transport;

    internal sealed class InMemoryDatagramChannel : IRpcDatagramChannel
    {
        private readonly Queue<byte[]> inboundDatagrams;
        private readonly List<byte[]> sentDatagrams;

        public InMemoryDatagramChannel(IEnumerable<byte[]> inboundDatagrams)
        {
            ArgumentNullException.ThrowIfNull(inboundDatagrams);

            this.inboundDatagrams = new Queue<byte[]>(CloneDatagrams(inboundDatagrams));
            sentDatagrams = new List<byte[]>();
        }

        public IReadOnlyList<byte[]> SentDatagrams => sentDatagrams;

        public ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (inboundDatagrams.Count == 0)
            {
                throw new InvalidOperationException("No inbound datagram is queued for the test channel.");
            }

            return ValueTask.FromResult(inboundDatagrams.Dequeue());
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentDatagrams.Add(datagram.ToArray());
            return ValueTask.CompletedTask;
        }

        private static IEnumerable<byte[]> CloneDatagrams(IEnumerable<byte[]> datagrams)
        {
            foreach (byte[] datagram in datagrams)
            {
                ArgumentNullException.ThrowIfNull(datagram);
                yield return (byte[])datagram.Clone();
            }
        }
    }
}
