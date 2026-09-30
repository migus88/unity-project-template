using System;
using R3;

namespace Core.Time
{
    public interface ITickSource
    {
        Observable<Unit> EverySecond { get; }
        Observable<Unit> EveryMinute { get; }

        ReadOnlyReactiveProperty<TimeSpan> CountdownTo(DateTime utcEnd);
    }
}
