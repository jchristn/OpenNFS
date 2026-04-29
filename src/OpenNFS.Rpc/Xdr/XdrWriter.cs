namespace OpenNFS.Rpc.Xdr
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Writes XDR-encoded data to an in-memory buffer.
    /// </summary>
    public sealed class XdrWriter
    {
        private static readonly byte[] PaddingBytes = new byte[3];
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        private readonly MemoryStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="XdrWriter"/> class.
        /// </summary>
        public XdrWriter()
        {
            stream = new MemoryStream();
        }

        /// <summary>
        /// Gets the current write offset.
        /// </summary>
        public int Position => checked((int)stream.Position);

        /// <summary>
        /// Writes an XDR boolean value.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteBoolean(bool value)
        {
            WriteUInt32(value ? 1U : 0U);
        }

        /// <summary>
        /// Writes an XDR signed 32-bit integer.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteInt32(int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes an XDR unsigned 32-bit integer.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteUInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes an XDR signed 64-bit integer.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteInt64(long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes an XDR unsigned 64-bit integer.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteUInt64(ulong value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            stream.Write(buffer);
        }

        /// <summary>
        /// Writes an XDR single-precision floating-point value.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteSingle(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        /// <summary>
        /// Writes an XDR double-precision floating-point value.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// Writes an XDR fixed-length opaque byte sequence.
        /// </summary>
        /// <param name="value">The opaque bytes to encode.</param>
        public void WriteFixedOpaque(ReadOnlySpan<byte> value)
        {
            stream.Write(value);
            WritePadding(value.Length);
        }

        /// <summary>
        /// Writes an XDR variable-length opaque byte sequence.
        /// </summary>
        /// <param name="value">The opaque bytes to encode.</param>
        /// <param name="maximumLength">The optional maximum allowed byte length.</param>
        public void WriteVariableOpaque(ReadOnlySpan<byte> value, uint? maximumLength = null)
        {
            ValidateMaximumLength(value.Length, maximumLength, nameof(value), "opaque value");
            WriteUInt32(checked((uint)value.Length));
            WriteFixedOpaque(value);
        }

        /// <summary>
        /// Writes an XDR UTF-8 string.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <param name="maximumUtf8ByteLength">The optional maximum allowed UTF-8 byte length.</param>
        public void WriteString(string value, uint? maximumUtf8ByteLength = null)
        {
            ArgumentNullException.ThrowIfNull(value);

            byte[] encodedValue;
            try
            {
                encodedValue = Utf8Encoding.GetBytes(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw new ArgumentException("The supplied string contains invalid UTF-16 data.", nameof(value), exception);
            }

            WriteVariableOpaque(encodedValue, maximumUtf8ByteLength);
        }

        /// <summary>
        /// Writes an XDR fixed-length array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The values to encode.</param>
        /// <param name="expectedCount">The required element count.</param>
        /// <param name="writeElement">The delegate used to encode each element.</param>
        public void WriteFixedArray<T>(IReadOnlyList<T> values, int expectedCount, Action<XdrWriter, T> writeElement)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(writeElement);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);

            if (values.Count != expectedCount)
            {
                throw new ArgumentException(
                    "The fixed-length array expected " + expectedCount + " element(s), but received " + values.Count + ".",
                    nameof(values));
            }

            for (int index = 0; index < values.Count; index++)
            {
                writeElement(this, values[index]);
            }
        }

        /// <summary>
        /// Writes an XDR variable-length array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The values to encode.</param>
        /// <param name="maximumCount">The optional maximum allowed element count.</param>
        /// <param name="writeElement">The delegate used to encode each element.</param>
        public void WriteVariableArray<T>(IReadOnlyList<T> values, uint? maximumCount, Action<XdrWriter, T> writeElement)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(writeElement);

            ValidateMaximumLength(values.Count, maximumCount, nameof(values), "array");
            WriteUInt32(checked((uint)values.Count));

            for (int index = 0; index < values.Count; index++)
            {
                writeElement(this, values[index]);
            }
        }

        /// <summary>
        /// Writes an XDR discriminated union.
        /// </summary>
        /// <typeparam name="TDiscriminant">The discriminant type.</typeparam>
        /// <param name="discriminant">The discriminant value.</param>
        /// <param name="writeDiscriminant">The delegate used to encode the discriminant.</param>
        /// <param name="writeArm">The delegate used to encode the selected arm.</param>
        public void WriteDiscriminatedUnion<TDiscriminant>(
            TDiscriminant discriminant,
            Action<XdrWriter, TDiscriminant> writeDiscriminant,
            Action<XdrWriter, TDiscriminant> writeArm)
        {
            ArgumentNullException.ThrowIfNull(writeDiscriminant);
            ArgumentNullException.ThrowIfNull(writeArm);

            writeDiscriminant(this, discriminant);
            writeArm(this, discriminant);
        }

        /// <summary>
        /// Returns the encoded XDR payload as a byte array.
        /// </summary>
        /// <returns>The encoded bytes.</returns>
        public byte[] ToArray()
        {
            return stream.ToArray();
        }

        private static void ValidateMaximumLength(int actualLength, uint? maximumLength, string parameterName, string subject)
        {
            if (maximumLength.HasValue && actualLength > maximumLength.Value)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "The " + subject + " length " + actualLength + " exceeds the maximum allowed length " + maximumLength.Value + ".");
            }
        }

        private void WritePadding(int unpaddedLength)
        {
            int paddingLength = XdrPadding.GetPaddingLength(unpaddedLength);
            if (paddingLength == 0)
            {
                return;
            }

            stream.Write(PaddingBytes.AsSpan(0, paddingLength));
        }
    }
}
