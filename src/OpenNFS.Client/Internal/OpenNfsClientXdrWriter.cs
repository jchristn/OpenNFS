namespace OpenNFS.Client.Internal
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Text;

    internal sealed class OpenNfsClientXdrWriter
    {
        private readonly MemoryStream _stream = new MemoryStream();

        internal void WriteInt32(int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        internal void WriteUInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        internal void WriteUInt64(ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            _stream.Write(buffer);
        }

        internal void WriteOpaque(byte[] value)
        {
            WriteUInt32((uint)value.Length);
            _stream.Write(value);
            WritePadding(value.Length);
        }

        internal void WriteFixedOpaque(byte[] value)
        {
            _stream.Write(value);
            WritePadding(value.Length);
        }

        internal void WriteString(string value)
        {
            WriteOpaque(Encoding.UTF8.GetBytes(value));
        }

        internal void WriteBytes(ReadOnlySpan<byte> value)
        {
            _stream.Write(value);
        }

        internal byte[] ToArray()
        {
            return _stream.ToArray();
        }

        private void WritePadding(int length)
        {
            int paddingLength = (4 - (length % 4)) % 4;

            if (paddingLength < 1)
            {
                return;
            }

            Span<byte> padding = stackalloc byte[3];
            _stream.Write(padding[..paddingLength]);
        }
    }
}
