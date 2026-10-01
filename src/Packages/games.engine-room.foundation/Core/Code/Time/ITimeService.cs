using System;
using R3;

namespace Core.Time
{
    public interface ITimeService
    {
        ReadOnlyReactiveProperty<bool> IsPaused { get; }
        float TimeScale { get; set; }

        IDisposable Pause();
    }
}
