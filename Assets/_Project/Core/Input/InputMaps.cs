using System;

namespace Core.Input
{
    [Flags]
    public enum InputMaps
    {
        None = 0,
        Player = 1 << 0,
        Ui = 1 << 1,
    }
}
