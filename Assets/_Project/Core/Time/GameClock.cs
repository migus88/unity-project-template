using System;

namespace Core.Time
{
    public sealed class GameClock : IGameClock
    {
        public DateTime UtcNow => _startUtc + TimeSpan.FromTicks((long)((UnityEngine.Time.timeAsDouble - _startGameTime) * TimeSpan.TicksPerSecond));

        private readonly DateTime _startUtc;
        private readonly double _startGameTime;

        public GameClock(IRealClock realClock)
        {
            _startUtc = realClock.UtcNow;
            _startGameTime = UnityEngine.Time.timeAsDouble;
        }
    }
}
