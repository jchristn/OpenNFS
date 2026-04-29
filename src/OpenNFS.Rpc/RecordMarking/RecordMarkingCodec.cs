namespace OpenNFS.Rpc.RecordMarking
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Encodes and decodes RFC 5531 record-marked message streams.
    /// </summary>
    public static class RecordMarkingCodec
    {
        /// <summary>
        /// Gets the RFC 5531 record-marking fragment-header length in bytes.
        /// </summary>
        public const int HeaderLength = 4;

        /// <summary>
        /// Gets the RFC 5531 maximum fragment payload length.
        /// </summary>
        public const int MaximumFragmentLength = 0x7FFFFFFF;

        /// <summary>
        /// Decodes a single RFC 5531 fragment header from the supplied buffer.
        /// </summary>
        /// <param name="buffer">The buffer beginning with a record-marking fragment header.</param>
        /// <returns>The decoded fragment header.</returns>
        public static RecordMarkingFragmentHeader ReadHeader(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < HeaderLength)
            {
                throw new InvalidDataException("A record-marking fragment header requires exactly 4 bytes.");
            }

            uint rawHeader = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, HeaderLength));
            bool isLastFragment = (rawHeader & 0x80000000U) != 0;
            int fragmentLength = checked((int)(rawHeader & 0x7FFFFFFFU));
            return new RecordMarkingFragmentHeader(isLastFragment, fragmentLength);
        }

        /// <summary>
        /// Encodes a single message payload into RFC 5531 record-marked fragments.
        /// </summary>
        /// <param name="messagePayload">The message payload to fragment.</param>
        /// <param name="maximumFragmentPayloadLength">The maximum payload length allowed for each fragment.</param>
        /// <returns>The record-marked byte stream.</returns>
        public static byte[] EncodeMessage(ReadOnlyMemory<byte> messagePayload, int maximumFragmentPayloadLength = MaximumFragmentLength)
        {
            if (maximumFragmentPayloadLength < 1 || maximumFragmentPayloadLength > MaximumFragmentLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumFragmentPayloadLength),
                    maximumFragmentPayloadLength,
                    "The fragment payload length must be between 1 and " + MaximumFragmentLength + ".");
            }

            int fragmentCount;
            if (messagePayload.Length == 0)
            {
                fragmentCount = 1;
            }
            else
            {
                fragmentCount = checked((messagePayload.Length + maximumFragmentPayloadLength - 1) / maximumFragmentPayloadLength);
            }

            int totalLength = checked(messagePayload.Length + (fragmentCount * HeaderLength));
            byte[] encoded = new byte[totalLength];

            if (messagePayload.Length == 0)
            {
                WriteHeader(encoded.AsSpan(0, HeaderLength), new RecordMarkingFragmentHeader(isLastFragment: true, fragmentLength: 0));
                return encoded;
            }

            int sourceOffset = 0;
            int destinationOffset = 0;
            while (sourceOffset < messagePayload.Length)
            {
                int fragmentLength = Math.Min(maximumFragmentPayloadLength, messagePayload.Length - sourceOffset);
                bool isLastFragment = sourceOffset + fragmentLength == messagePayload.Length;
                WriteHeader(
                    encoded.AsSpan(destinationOffset, HeaderLength),
                    new RecordMarkingFragmentHeader(isLastFragment, fragmentLength));
                destinationOffset += HeaderLength;

                messagePayload.Span.Slice(sourceOffset, fragmentLength).CopyTo(encoded.AsSpan(destinationOffset, fragmentLength));
                destinationOffset += fragmentLength;
                sourceOffset += fragmentLength;
            }

            return encoded;
        }

        /// <summary>
        /// Decodes one or more record-marked messages from the supplied byte stream.
        /// </summary>
        /// <param name="recordMarkedPayload">The record-marked byte stream.</param>
        /// <returns>The reassembled message payloads in wire order.</returns>
        public static IReadOnlyList<byte[]> DecodeMessages(ReadOnlyMemory<byte> recordMarkedPayload)
        {
            List<byte[]> messages = new List<byte[]>();
            MemoryStream currentMessage = new MemoryStream();
            int offset = 0;
            bool expectingContinuation = false;

            while (offset < recordMarkedPayload.Length)
            {
                RecordMarkingFragmentHeader header = ReadHeader(recordMarkedPayload.Span.Slice(offset));
                offset += HeaderLength;

                if (recordMarkedPayload.Length - offset < header.FragmentLength)
                {
                    throw new InvalidDataException(
                        "The record-marked payload ended before fragment data of length " + header.FragmentLength + " was fully available.");
                }

                if (header.FragmentLength != 0)
                {
                    currentMessage.Write(recordMarkedPayload.Span.Slice(offset, header.FragmentLength));
                    offset += header.FragmentLength;
                }

                expectingContinuation = !header.IsLastFragment;
                if (header.IsLastFragment)
                {
                    messages.Add(currentMessage.ToArray());
                    currentMessage.SetLength(0);
                }
            }

            currentMessage.Dispose();

            if (expectingContinuation)
            {
                throw new InvalidDataException("The record-marked payload ended before the current record reached a last fragment.");
            }

            return messages;
        }

        /// <summary>
        /// Decodes exactly one record-marked message from the supplied byte stream.
        /// </summary>
        /// <param name="recordMarkedPayload">The record-marked byte stream.</param>
        /// <returns>The reassembled message payload.</returns>
        public static byte[] DecodeSingleMessage(ReadOnlyMemory<byte> recordMarkedPayload)
        {
            IReadOnlyList<byte[]> messages = DecodeMessages(recordMarkedPayload);
            if (messages.Count != 1)
            {
                throw new InvalidDataException("Expected exactly one record-marked message but decoded " + messages.Count + ".");
            }

            return messages[0];
        }

        private static void WriteHeader(Span<byte> destination, RecordMarkingFragmentHeader header)
        {
            uint rawHeader = checked((uint)header.FragmentLength);
            if (header.IsLastFragment)
            {
                rawHeader |= 0x80000000U;
            }

            BinaryPrimitives.WriteUInt32BigEndian(destination, rawHeader);
        }
    }
}
