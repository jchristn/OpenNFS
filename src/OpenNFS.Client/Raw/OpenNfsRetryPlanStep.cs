namespace OpenNFS.Client.Raw
{
    using System;

    /// <summary>
    /// Planned retry timing for a raw client attempt.
    /// </summary>
    public sealed class OpenNfsRetryPlanStep
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsRetryPlanStep"/> class.
        /// </summary>
        /// <param name="attemptNumber">
        /// One-based attempt number.
        /// Minimum value: <c>1</c>.
        /// </param>
        /// <param name="delayBeforeAttempt">
        /// Delay before issuing the attempt.
        /// Minimum value: <c>TimeSpan.Zero</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an attempt-planning value is outside the supported range.</exception>
        public OpenNfsRetryPlanStep(int attemptNumber, TimeSpan delayBeforeAttempt)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "The retry-plan attempt number must be at least 1.");
            }

            if (delayBeforeAttempt < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delayBeforeAttempt), delayBeforeAttempt, "The retry-plan delay must be zero or greater.");
            }

            AttemptNumber = attemptNumber;
            DelayBeforeAttempt = delayBeforeAttempt;
        }

        /// <summary>
        /// Gets the one-based attempt number.
        /// </summary>
        public int AttemptNumber { get; }

        /// <summary>
        /// Gets the delay before issuing the attempt.
        /// </summary>
        public TimeSpan DelayBeforeAttempt { get; }
    }
}
