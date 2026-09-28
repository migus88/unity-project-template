using System;

namespace Core.Time
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
