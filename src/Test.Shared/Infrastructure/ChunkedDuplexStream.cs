namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ChunkedDuplexStream : Stream
    {
        private readonly byte[] inboundBytes;
        private readonly IReadOnlyList<int> readSizes;
        private readonly MemoryStream outboundBytes;
        private int inboundOffset;
        private int readCallCount;

        public ChunkedDuplexStream(byte[] inboundBytes, IReadOnlyList<int> readSizes)
        {
            ArgumentNullException.ThrowIfNull(inboundBytes);
            ArgumentNullException.ThrowIfNull(readSizes);

            this.inboundBytes = inboundBytes;
            this.readSizes = readSizes;
            outboundBytes = new MemoryStream();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] WrittenBytes => outboundBytes.ToArray();

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Synchronous reads are not used by this test helper.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Synchronous writes are not used by this test helper.");
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (inboundOffset >= inboundBytes.Length)
            {
                return ValueTask.FromResult(0);
            }

            int configuredReadSize = buffer.Length;
            if (readCallCount < readSizes.Count)
            {
                configuredReadSize = readSizes[readCallCount];
            }

            readCallCount++;
            int bytesToCopy = Math.Min(buffer.Length, configuredReadSize);
            bytesToCopy = Math.Min(bytesToCopy, inboundBytes.Length - inboundOffset);
            inboundBytes.AsSpan(inboundOffset, bytesToCopy).CopyTo(buffer.Span);
            inboundOffset += bytesToCopy;
            return ValueTask.FromResult(bytesToCopy);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outboundBytes.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
