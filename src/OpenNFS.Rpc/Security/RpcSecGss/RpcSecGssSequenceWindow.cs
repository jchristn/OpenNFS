namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Implements the RFC 2203 §5.3.3.1 sequence-number window used by the server to detect replays.
    /// </summary>
    /// <remarks>
    /// Each established context tracks the highest sequence number observed plus a fixed-size bitmap
    /// of recent values. A request whose sequence number falls below the window or has already been
    /// observed is rejected. A request whose sequence number is above the current top slides the
    /// window forward and clears the slots that fall out.
    /// </remarks>
    public sealed class RpcSecGssSequenceWindow
    {
        private readonly object gate;
        private readonly bool[] observed;
        private uint highestSequence;
        private bool isInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssSequenceWindow"/> class.
        /// </summary>
        /// <param name="windowSize">The number of recent sequence numbers tracked.</param>
        public RpcSecGssSequenceWindow(uint windowSize)
        {
            if (windowSize == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "The sequence window size must be at least 1.");
            }

            gate = new object();
            observed = new bool[windowSize];
            WindowSize = windowSize;
        }

        /// <summary>
        /// Gets the configured window size.
        /// </summary>
        public uint WindowSize { get; }

        /// <summary>
        /// Gets the highest sequence number currently observed.
        /// </summary>
        public uint HighestObservedSequence
        {
            get
            {
                lock (gate)
                {
                    return highestSequence;
                }
            }
        }

        /// <summary>
        /// Attempts to accept <paramref name="sequenceNumber"/> as a fresh request.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number presented by the client.</param>
        /// <returns>
        /// <c>true</c> when the sequence number is fresh and was recorded; <c>false</c> when it has
        /// already been observed or has fallen below the window.
        /// </returns>
        public bool TryAccept(uint sequenceNumber)
        {
            if (sequenceNumber > RpcSecGssProtocolConstants.MaximumSequenceNumber)
            {
                return false;
            }

            lock (gate)
            {
                if (!isInitialized)
                {
                    isInitialized = true;
                    highestSequence = sequenceNumber;
                    Array.Clear(observed, 0, observed.Length);
                    observed[(int)(sequenceNumber % WindowSize)] = true;
                    return true;
                }

                if (sequenceNumber > highestSequence)
                {
                    uint advanceBy = sequenceNumber - highestSequence;
                    if (advanceBy >= WindowSize)
                    {
                        Array.Clear(observed, 0, observed.Length);
                    }
                    else
                    {
                        for (uint step = 1; step <= advanceBy; step++)
                        {
                            int staleSlot = (int)((highestSequence + step) % WindowSize);
                            observed[staleSlot] = false;
                        }
                    }

                    highestSequence = sequenceNumber;
                    observed[(int)(sequenceNumber % WindowSize)] = true;
                    return true;
                }

                if (highestSequence - sequenceNumber >= WindowSize)
                {
                    return false;
                }

                int slot = (int)(sequenceNumber % WindowSize);
                if (observed[slot])
                {
                    return false;
                }

                observed[slot] = true;
                return true;
            }
        }
    }
}
