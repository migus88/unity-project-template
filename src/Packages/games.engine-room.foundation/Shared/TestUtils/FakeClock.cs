using System;
using Core.Time;

namespace TestUtils
{
    public sealed class FakeClock : IRealClock, IGameClock
    {
        public DateTime UtcNow { get; set; }

        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            UtcNow += delta;
        }
    }
}
