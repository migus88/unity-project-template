using UnityEngine;

namespace Core
{
    public interface IApplicationService
    {
        string Version { get; }
        RuntimePlatform Platform { get; }

        void Quit();
    }
}
