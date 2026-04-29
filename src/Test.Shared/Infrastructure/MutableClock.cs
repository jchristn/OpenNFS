namespace Test.Shared.Infrastructure
{
    using System;

    internal sealed class MutableClock
    {
        internal MutableClock(DateTimeOffset currentUtc)
        {
            CurrentUtc = currentUtc;
        }

        internal DateTimeOffset CurrentUtc { get; private set; }

        internal DateTimeOffset UtcNow()
        {
            return CurrentUtc;
        }

        internal void Advance(TimeSpan duration)
        {
            CurrentUtc = CurrentUtc.Add(duration);
        }
    }
}
