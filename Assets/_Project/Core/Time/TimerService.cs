using System;
using VContainer.Unity;

namespace Core.Time
{
    public sealed class TimerService : ITimerService, ITickable, IDisposable
    {
        public ITickSource Real => _real;
        public ITickSource Game => _game;

        private readonly TickSource _real;
        private readonly TickSource _game;

        public TimerService(IRealClock realClock, IGameClock gameClock)
        {
            _real = new TickSource(realClock);
            _game = new TickSource(gameClock);
        }

        public void Tick()
        {
            _real.Tick();
            _game.Tick();
        }

        public void Dispose()
        {
            _real.Dispose();
            _game.Dispose();
        }
    }
}
