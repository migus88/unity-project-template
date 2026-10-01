using System;

namespace Core.Input
{
    [Flags]
    public enum InputLockTag
    {
        None = 0,
        Movement = 1 << 0,
        Camera = 1 << 1,
        Interaction = 1 << 2,
        Ui = 1 << 3,
        Pause = 1 << 4,
    }
}
