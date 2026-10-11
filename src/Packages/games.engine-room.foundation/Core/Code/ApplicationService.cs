using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Core
{
    public sealed class ApplicationService : IApplicationService, IInitializable, IDisposable
    {
        public static readonly TimeSpan DefaultQuitBudget = TimeSpan.FromSeconds(2);

        public string Version => Application.version;
        public RuntimePlatform Platform => Application.platform;
        public TimeSpan QuitBudget => DefaultQuitBudget;

        private bool _isQuitStarted;
        private bool _isQuitCompleted;

        private readonly QuitHandlers _quitHandlers = new();

        public void Initialize()
        {
#if UNITY_EDITOR
            Application.quitting += OnQuitting;
#else
            Application.wantsToQuit += OnWantsToQuit;
#endif
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public IDisposable AddQuitHandler(Func<CancellationToken, UniTask> handler)
        {
            return _quitHandlers.Add(handler);
        }

        private void OnQuitting()
        {
            _quitHandlers.RunAsync(QuitBudget, CancellationToken.None).Forget();
        }

        private bool OnWantsToQuit()
        {
            if (_isQuitCompleted)
            {
                return true;
            }

            if (!_isQuitStarted)
            {
                _isQuitStarted = true;
                RunHandlersThenQuitAsync().Forget();
            }

            return false;
        }

        private async UniTaskVoid RunHandlersThenQuitAsync()
        {
            try
            {
                await _quitHandlers.RunAsync(QuitBudget, CancellationToken.None);
            }
            finally
            {
                _isQuitCompleted = true;
                Application.Quit();
            }
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            Application.quitting -= OnQuitting;
#else
            Application.wantsToQuit -= OnWantsToQuit;
#endif
        }
    }
}
