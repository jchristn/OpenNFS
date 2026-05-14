namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;

    internal static class OpenNfsV40ReplyValueReader
    {
        internal static uint[] ReadBitmapWords(bitmap4? bitmap, string fieldName)
        {
            if (bitmap?.Value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return bitmap.Value.AsSpan().ToArray();
        }

        internal static T ReadRequiredEnum<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static byte[] ReadRequiredFixedOpaque(byte[]? value, string fieldName, int expectedLength)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (value.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "The decoded " + fieldName + " field must contain exactly " + expectedLength + " byte(s).");
            }

            return value.AsSpan().ToArray();
        }

        internal static ReadOnlyMemory<byte> ReadRequiredOpaque(byte[]? value, string fieldName, bool allowEmpty = false)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && value.Length < 1)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return new ReadOnlyMemory<byte>(value.AsSpan().ToArray());
        }

        internal static ulong ReadRequiredUInt64(ulong? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static uint ReadRequiredUInt32(uint? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        internal static string ReadRequiredUtf8(byte[]? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return Encoding.UTF8.GetString(value);
        }
    }
}
