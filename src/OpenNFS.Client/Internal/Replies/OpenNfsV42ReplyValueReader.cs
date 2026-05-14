namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;

    internal static class OpenNfsV42ReplyValueReader
    {
        internal static uint[] ReadBitmapWords(OpenNFS.Protocol.V42.Generated.bitmap4? bitmap, string fieldName)
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

        internal static ReadOnlyMemory<byte> ReadRequiredFixedOpaque(byte[]? value, string fieldName, int expectedLength)
        {
            ReadOnlyMemory<byte> bytes = ReadRequiredOpaque(value, fieldName, allowEmpty: false);
            if (bytes.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "The decoded "
                    + fieldName
                    + " field was expected to contain exactly "
                    + expectedLength
                    + " bytes but contained "
                    + bytes.Length
                    + ".");
            }

            return bytes;
        }

        internal static ulong ReadRequiredUInt64(ulong? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }
    }
}
