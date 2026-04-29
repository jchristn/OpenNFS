namespace OpenNFS.Rpc.RecordMarking
{
    using System;

    /// <summary>
    /// Represents a single RFC 5531 record-marking fragment header.
    /// </summary>
    public readonly struct RecordMarkingFragmentHeader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecordMarkingFragmentHeader"/> struct.
        /// </summary>
        /// <param name="isLastFragment">True when the fragment terminates the current record.</param>
        /// <param name="fragmentLength">The fragment payload length in bytes.</param>
        public RecordMarkingFragmentHeader(bool isLastFragment, int fragmentLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(fragmentLength);

            if (fragmentLength > RecordMarkingCodec.MaximumFragmentLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fragmentLength),
                    fragmentLength,
                    "The fragment length exceeds the RFC 5531 record-marking maximum.");
            }

            IsLastFragment = isLastFragment;
            FragmentLength = fragmentLength;
        }

        /// <summary>
        /// Gets a value indicating whether the fragment terminates the current record.
        /// </summary>
        public bool IsLastFragment { get; }

        /// <summary>
        /// Gets the fragment payload length in bytes.
        /// </summary>
        public int FragmentLength { get; }
    }
}
