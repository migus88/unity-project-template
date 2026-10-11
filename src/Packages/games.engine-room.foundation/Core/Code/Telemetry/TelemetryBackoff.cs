using System;

namespace Core.Telemetry
{
    internal sealed class TelemetryBackoff
    {
        public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromHours(1);

        private int _attempts;
        private DateTime _retryAt = DateTime.MinValue;

        public static TimeSpan Delay(int attempt, TimeSpan? retryAfter)
        {
            var exponent = Math.Min(Math.Max(attempt - 1, 0), 16);
            var delay = TimeSpan.FromTicks(Math.Min(BaseDelay.Ticks << exponent, MaxDelay.Ticks));

            if (retryAfter.HasValue && retryAfter.Value > delay)
            {
                return retryAfter.Value < MaxRetryAfter ? retryAfter.Value : MaxRetryAfter;
            }

            return delay;
        }

        public bool CanSend(DateTime now)
        {
            return now >= _retryAt;
        }

        public void Fail(DateTime now, TimeSpan? retryAfter)
        {
            _attempts++;
            _retryAt = now + Delay(_attempts, retryAfter);
        }

        public void Reset()
        {
            _attempts = 0;
            _retryAt = DateTime.MinValue;
        }
    }
}
