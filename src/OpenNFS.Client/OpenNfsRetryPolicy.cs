namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Retry policy exposed by the public OpenNFS client surface.
    /// </summary>
    public sealed class OpenNfsRetryPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsRetryPolicy"/> class.
        /// </summary>
        /// <param name="maximumAttempts">
        /// Maximum total attempt count, including the initial attempt.
        /// Default value: <c>3</c>.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>32</c>.
        /// </param>
        /// <param name="initialDelay">
        /// Delay before the first retry.
        /// Default value: <c>00:00:00.2500000</c>.
        /// Minimum value: greater than <c>TimeSpan.Zero</c>.
        /// Maximum value: <c>00:00:30</c>.
        /// </param>
        /// <param name="maximumDelay">
        /// Maximum delay between retries after backoff is applied.
        /// Default value: <c>00:00:02</c>.
        /// Minimum value: at least <paramref name="initialDelay"/>.
        /// Maximum value: <c>00:05:00</c>.
        /// </param>
        /// <param name="useExponentialBackoff">
        /// True to double the delay for successive retries up to <paramref name="maximumDelay"/>.
        /// Default value: <c>true</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a retry-policy value is outside the supported range.</exception>
        public OpenNfsRetryPolicy(
            int maximumAttempts = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maximumDelay = null,
            bool useExponentialBackoff = true)
        {
            if (maximumAttempts < 1 || maximumAttempts > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts), maximumAttempts, "The maximum retry attempt count must be between 1 and 32.");
            }

            TimeSpan resolvedInitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(250);
            TimeSpan resolvedMaximumDelay = maximumDelay ?? TimeSpan.FromSeconds(2);

            if (resolvedInitialDelay <= TimeSpan.Zero || resolvedInitialDelay > TimeSpan.FromSeconds(30))
            {
                throw new ArgumentOutOfRangeException(nameof(initialDelay), resolvedInitialDelay, "The initial retry delay must be greater than zero and no more than thirty seconds.");
            }

            if (resolvedMaximumDelay < resolvedInitialDelay || resolvedMaximumDelay > TimeSpan.FromMinutes(5))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDelay), resolvedMaximumDelay, "The maximum retry delay must be at least the initial delay and no more than five minutes.");
            }

            MaximumAttempts = maximumAttempts;
            InitialDelay = resolvedInitialDelay;
            MaximumDelay = resolvedMaximumDelay;
            UseExponentialBackoff = useExponentialBackoff;
        }

        /// <summary>
        /// Gets the maximum total attempt count, including the initial attempt.
        /// </summary>
        public int MaximumAttempts { get; }

        /// <summary>
        /// Gets the delay before the first retry.
        /// </summary>
        public TimeSpan InitialDelay { get; }

        /// <summary>
        /// Gets the maximum delay between retries after backoff is applied.
        /// </summary>
        public TimeSpan MaximumDelay { get; }

        /// <summary>
        /// Gets a value indicating whether exponential backoff is enabled.
        /// </summary>
        public bool UseExponentialBackoff { get; }

        /// <summary>
        /// Gets the planned delay before a given retry attempt.
        /// </summary>
        /// <param name="retryNumber">
        /// One-based retry number.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>MaximumAttempts - 1</c> when retries are enabled.
        /// </param>
        /// <returns>The planned delay before the requested retry attempt.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="retryNumber"/> is outside the supported range.</exception>
        public TimeSpan GetDelayForRetry(int retryNumber)
        {
            int maximumRetryNumber = Math.Max(0, MaximumAttempts - 1);
            if (retryNumber < 1 || retryNumber > maximumRetryNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retryNumber),
                    retryNumber,
                    "The retry number must be between 1 and " + maximumRetryNumber + " for the configured retry policy.");
            }

            if (!UseExponentialBackoff)
            {
                return InitialDelay;
            }

            double multiplier = Math.Pow(2d, retryNumber - 1);
            double scaledTicks = InitialDelay.Ticks * multiplier;
            double boundedTicks = Math.Min(scaledTicks, MaximumDelay.Ticks);
            return TimeSpan.FromTicks(Convert.ToInt64(boundedTicks));
        }
    }
}
