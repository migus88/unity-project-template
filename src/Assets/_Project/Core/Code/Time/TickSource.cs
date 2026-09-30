using System;
using System.Collections.Generic;
using R3;

namespace Core.Time
{
    internal sealed class TickSource : ITickSource, IDisposable
    {
        private const long SecondsPerMinute = 60;

        public Observable<Unit> EverySecond => _everySecond;
        public Observable<Unit> EveryMinute => _everyMinute;

        private long _lastSecond;

        private readonly IClock _clock;
        private readonly Subject<Unit> _everySecond = new();
        private readonly Subject<Unit> _everyMinute = new();
        private readonly List<Countdown> _countdowns = new();

        public TickSource(IClock clock)
        {
            _clock = clock;
            _lastSecond = ToWholeSeconds(clock.UtcNow);
        }

        public ReadOnlyReactiveProperty<TimeSpan> CountdownTo(DateTime utcEnd)
        {
            var remaining = new ReactiveProperty<TimeSpan>(GetRemaining(utcEnd, _clock.UtcNow));

            if (remaining.Value > TimeSpan.Zero)
            {
                _countdowns.Add(new Countdown(utcEnd, remaining));
            }

            return remaining;
        }

        public void Tick()
        {
            var now = _clock.UtcNow;
            var second = ToWholeSeconds(now);

            if (second == _lastSecond)
            {
                return;
            }

            var previousMinute = _lastSecond / SecondsPerMinute;
            _lastSecond = second;

            UpdateCountdowns(now);
            _everySecond.OnNext(Unit.Default);

            if (second / SecondsPerMinute != previousMinute)
            {
                _everyMinute.OnNext(Unit.Default);
            }
        }

        private void UpdateCountdowns(DateTime now)
        {
            for (var i = _countdowns.Count - 1; i >= 0; i--)
            {
                var countdown = _countdowns[i];

                if (countdown.Remaining.IsDisposed)
                {
                    _countdowns.RemoveAt(i);
                    continue;
                }

                var remaining = GetRemaining(countdown.UtcEnd, now);

                if (remaining == TimeSpan.Zero)
                {
                    _countdowns.RemoveAt(i);
                }

                countdown.Remaining.Value = remaining;
            }
        }

        private static TimeSpan GetRemaining(DateTime utcEnd, DateTime now)
        {
            var remaining = utcEnd - now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private static long ToWholeSeconds(DateTime utc)
        {
            return utc.Ticks / TimeSpan.TicksPerSecond;
        }

        public void Dispose()
        {
            foreach (var countdown in _countdowns)
            {
                countdown.Remaining.Dispose();
            }

            _countdowns.Clear();
            _everySecond.Dispose();
            _everyMinute.Dispose();
        }

        private readonly record struct Countdown(DateTime UtcEnd, ReactiveProperty<TimeSpan> Remaining);
    }
}
