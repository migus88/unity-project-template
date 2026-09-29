using System;

namespace Core.Time
{
    public sealed class RealClock : IRealClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
