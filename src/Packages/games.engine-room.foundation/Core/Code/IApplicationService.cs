using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public interface IApplicationService
    {
        string Version { get; }
        RuntimePlatform Platform { get; }
        TimeSpan QuitBudget { get; }

        void Quit();
        IDisposable AddQuitHandler(Func<CancellationToken, UniTask> handler);
        void OpenUrl(string url);
    }
}
