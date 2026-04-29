namespace OpenNFS.Rpc.Xdr
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Reads XDR-encoded data from an in-memory buffer.
    /// </summary>
    public sealed class XdrReader
    {
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        private readonly ReadOnlyMemory<byte> buffer;
        private int offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="XdrReader"/> class.
        /// </summary>
        /// <param name="buffer">The source buffer containing XDR-encoded data.</param>
        public XdrReader(ReadOnlyMemory<byte> buffer)
        {
            this.buffer = buffer;
        }

        /// <summary>
        /// Gets the current read offset.
        /// </summary>
        public int Position => offset;

        /// <summary>
        /// Gets the number of unread bytes remaining in the source buffer.
        /// </summary>
        public int RemainingLength => buffer.Length - offset;

        /// <summary>
        /// Reads an XDR boolean value.
        /// </summary>
        /// <returns>The decoded boolean value.</returns>
        public bool ReadBoolean()
        {
            int position = Position;
            uint rawValue = ReadUInt32();
            if (rawValue == 0)
            {
                return false;
            }

            if (rawValue == 1)
            {
                return true;
            }

            throw new XdrDataException("The encoded boolean value must be 0 or 1.", position);
        }

        /// <summary>
        /// Reads an XDR signed 32-bit integer.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public int ReadInt32()
        {
            return BinaryPrimitives.ReadInt32BigEndian(ReadRequiredSpan(4, "signed 32-bit integer"));
        }

        /// <summary>
        /// Reads an XDR unsigned 32-bit integer.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public uint ReadUInt32()
        {
            return BinaryPrimitives.ReadUInt32BigEndian(ReadRequiredSpan(4, "unsigned 32-bit integer"));
        }

        /// <summary>
        /// Reads an XDR signed 64-bit integer.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public long ReadInt64()
        {
            return BinaryPrimitives.ReadInt64BigEndian(ReadRequiredSpan(8, "signed 64-bit integer"));
        }

        /// <summary>
        /// Reads an XDR unsigned 64-bit integer.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public ulong ReadUInt64()
        {
            return BinaryPrimitives.ReadUInt64BigEndian(ReadRequiredSpan(8, "unsigned 64-bit integer"));
        }

        /// <summary>
        /// Reads an XDR single-precision floating-point value.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public float ReadSingle()
        {
            int bits = BinaryPrimitives.ReadInt32BigEndian(ReadRequiredSpan(4, "single-precision floating-point value"));
            return BitConverter.Int32BitsToSingle(bits);
        }

        /// <summary>
        /// Reads an XDR double-precision floating-point value.
        /// </summary>
        /// <returns>The decoded value.</returns>
        public double ReadDouble()
        {
            long bits = BinaryPrimitives.ReadInt64BigEndian(ReadRequiredSpan(8, "double-precision floating-point value"));
            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>
        /// Reads an XDR fixed-length opaque byte sequence.
        /// </summary>
        /// <param name="length">The fixed byte length.</param>
        /// <returns>The decoded opaque bytes.</returns>
        public byte[] ReadFixedOpaque(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            return ReadOpaqueBytes(length, "fixed opaque value");
        }

        /// <summary>
        /// Reads an XDR variable-length opaque byte sequence.
        /// </summary>
        /// <param name="maximumLength">The optional maximum allowed byte length.</param>
        /// <returns>The decoded opaque bytes.</returns>
        public byte[] ReadVariableOpaque(uint? maximumLength = null)
        {
            int position = Position;
            uint encodedLength = ReadUInt32();
            ValidateMaximumValue(encodedLength, maximumLength, position, "opaque value");
            int length = ConvertToInt32Length(encodedLength, position, "opaque value");
            return ReadOpaqueBytes(length, "opaque value");
        }

        /// <summary>
        /// Reads an XDR UTF-8 string.
        /// </summary>
        /// <param name="maximumUtf8ByteLength">The optional maximum allowed UTF-8 byte length.</param>
        /// <returns>The decoded string.</returns>
        public string ReadString(uint? maximumUtf8ByteLength = null)
        {
            int position = Position;
            byte[] encodedValue = ReadVariableOpaque(maximumUtf8ByteLength);

            try
            {
                return Utf8Encoding.GetString(encodedValue);
            }
            catch (DecoderFallbackException exception)
            {
                throw new XdrDataException("The encoded string contains invalid UTF-8 data.", position, exception);
            }
        }

        /// <summary>
        /// Reads an XDR fixed-length array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="expectedCount">The required element count.</param>
        /// <param name="readElement">The delegate used to decode each element.</param>
        /// <returns>The decoded array.</returns>
        public IReadOnlyList<T> ReadFixedArray<T>(int expectedCount, Func<XdrReader, T> readElement)
        {
            ArgumentNullException.ThrowIfNull(readElement);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);

            T[] values = new T[expectedCount];
            for (int index = 0; index < expectedCount; index++)
            {
                values[index] = readElement(this);
            }

            return values;
        }

        /// <summary>
        /// Reads an XDR variable-length array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="maximumCount">The optional maximum allowed element count.</param>
        /// <param name="readElement">The delegate used to decode each element.</param>
        /// <returns>The decoded array.</returns>
        public IReadOnlyList<T> ReadVariableArray<T>(uint? maximumCount, Func<XdrReader, T> readElement)
        {
            ArgumentNullException.ThrowIfNull(readElement);

            int position = Position;
            uint encodedCount = ReadUInt32();
            ValidateMaximumValue(encodedCount, maximumCount, position, "array");
            int count = ConvertToInt32Length(encodedCount, position, "array");

            T[] values = new T[count];
            for (int index = 0; index < count; index++)
            {
                values[index] = readElement(this);
            }

            return values;
        }

        /// <summary>
        /// Reads an XDR discriminated union.
        /// </summary>
        /// <typeparam name="TDiscriminant">The discriminant type.</typeparam>
        /// <typeparam name="TResult">The decoded result type.</typeparam>
        /// <param name="readDiscriminant">The delegate used to decode the discriminant.</param>
        /// <param name="readArm">The delegate used to decode the selected arm.</param>
        /// <returns>The decoded union result.</returns>
        public TResult ReadDiscriminatedUnion<TDiscriminant, TResult>(
            Func<XdrReader, TDiscriminant> readDiscriminant,
            Func<XdrReader, TDiscriminant, TResult> readArm)
        {
            ArgumentNullException.ThrowIfNull(readDiscriminant);
            ArgumentNullException.ThrowIfNull(readArm);

            TDiscriminant discriminant = readDiscriminant(this);
            return readArm(this, discriminant);
        }

        /// <summary>
        /// Ensures the reader has consumed the entire source buffer.
        /// </summary>
        public void EnsureFullyConsumed()
        {
            if (RemainingLength != 0)
            {
                throw new XdrDataException("Unread bytes remain after decoding completed.", Position);
            }
        }

        private static int ConvertToInt32Length(uint encodedValue, int position, string subject)
        {
            if (encodedValue > int.MaxValue)
            {
                throw new XdrDataException("The encoded " + subject + " length exceeds the supported in-memory limit.", position);
            }

            return checked((int)encodedValue);
        }

        private static void ValidateMaximumValue(uint actualValue, uint? maximumValue, int position, string subject)
        {
            if (maximumValue.HasValue && actualValue > maximumValue.Value)
            {
                throw new XdrDataException(
                    "The encoded " + subject + " length " + actualValue + " exceeds the maximum allowed length " + maximumValue.Value + ".",
                    position);
            }
        }

        private void EnsureAvailable(int count, string subject)
        {
            if (count > RemainingLength)
            {
                throw new XdrDataException("Insufficient data to read the " + subject + ".", Position);
            }
        }

        private byte[] ReadOpaqueBytes(int length, string subject)
        {
            EnsureAvailable(length, subject);

            byte[] value = new byte[length];
            if (length != 0)
            {
                ReadOnlySpan<byte> span = buffer.Span.Slice(offset, length);
                span.CopyTo(value);
                offset += length;
            }

            SkipPadding(length, subject);
            return value;
        }

        private ReadOnlySpan<byte> ReadRequiredSpan(int count, string subject)
        {
            EnsureAvailable(count, subject);
            ReadOnlySpan<byte> span = buffer.Span.Slice(offset, count);
            offset += count;
            return span;
        }

        private void SkipPadding(int unpaddedLength, string subject)
        {
            int paddingLength = XdrPadding.GetPaddingLength(unpaddedLength);
            if (paddingLength == 0)
            {
                return;
            }

            EnsureAvailable(paddingLength, subject + " padding");
            offset += paddingLength;
        }
    }
}
