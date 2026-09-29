using System;

namespace Core.Input
{
    public interface IInputService
    {
        GameInput Actions { get; }

        IDisposable Push(InputMaps maps);
    }
}
