namespace OpenNFS.Client.Internal
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only, seekable <see cref="Stream"/> over NFSv3 <c>READ</c> calls for a mounted-session file.
    /// <see cref="Length"/> is the file size observed when the stream was opened. Data is fetched lazily in
    /// transfer-size chunks and the most recent chunk is cached. Instances are not thread-safe.
    /// </summary>
    internal sealed class OpenNfsMountSessionReadStream : Stream
    {
        private const string OperationName = "Mounted-session stream read";

        private readonly int _chunkSize;
        private readonly byte[] _fileHandle;
        private readonly long _length;
        private readonly string _path;
        private readonly OpenNfsMountSession _session;
        private byte[]? _buffer;
        private int _bufferCount;
        private long _bufferOffset;
        private int _disposed;
        private long _position;

        internal OpenNfsMountSessionReadStream(
            OpenNfsMountSession session,
            byte[] fileHandle,
            string path,
            long length,
            int chunkSize)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(path);

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "The stream length cannot be negative.");
            }

            if (chunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "The chunk size must be positive.");
            }

            _session = session;
            _fileHandle = fileHandle;
            _path = path;
            _length = length;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => !IsDisposed;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return _length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return _position;
            }

            set
            {
                ThrowIfDisposed();
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The stream position cannot be negative.");
                }

                _position = value;
            }
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadCoreAsync(new Memory<byte>(buffer, offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                ThrowIfDisposed();
                return 0;
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int read = ReadCoreAsync(new Memory<byte>(rented, 0, buffer.Length), CancellationToken.None).AsTask().GetAwaiter().GetResult();
                rented.AsSpan(0, read).CopyTo(buffer);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadCoreAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ReadCoreAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();

            long basePosition = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => _position,
                SeekOrigin.End => _length,
                _ => throw new ArgumentException("The seek origin is not defined.", nameof(origin)),
            };

            long newPosition;
            try
            {
                newPosition = checked(basePosition + offset);
            }
            catch (OverflowException exception)
            {
                throw new IOException("The requested seek position is outside the supported range.", exception);
            }

            if (newPosition < 0)
            {
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");
            }

            _position = newPosition;
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Mounted-session read streams are read-only.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Mounted-session read streams are read-only.");
        }

        public override ValueTask DisposeAsync()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _buffer = null;
            _bufferCount = 0;

            try
            {
                base.Dispose(disposing);
            }
            catch (Exception)
            {
            }
        }

        private async ValueTask<int> ReadCoreAsync(Memory<byte> destination, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (destination.Length == 0 || _position >= _length)
            {
                return 0;
            }

            if (_buffer is null
                || _position < _bufferOffset
                || _position >= _bufferOffset + _bufferCount)
            {
                int fetchCount = (int)OpenNfsMountSessionReadSupport.AlignedReadCount(
                    (ulong)_position,
                    (int)Math.Min(_chunkSize, _length - _position),
                    _chunkSize);
                byte[] chunk = new byte[fetchCount];
                int fetched = await OpenNfsMountSessionReadSupport.ReadRangeAsync(
                    _session,
                    _fileHandle,
                    _path,
                    OperationName,
                    (ulong)_position,
                    chunk,
                    _chunkSize,
                    cancellationToken).ConfigureAwait(false);

                ThrowIfDisposed();
                if (fetched == 0)
                {
                    return 0;
                }

                _buffer = chunk;
                _bufferOffset = _position;
                _bufferCount = fetched;
            }

            int bufferIndex = (int)(_position - _bufferOffset);
            int available = _bufferCount - bufferIndex;
            int toCopy = Math.Min(available, destination.Length);
            _buffer.AsSpan(bufferIndex, toCopy).CopyTo(destination.Span);
            _position += toCopy;
            return toCopy;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsMountSessionReadStream), "The mounted-session read stream has been disposed.");
            }
        }
    }
}
