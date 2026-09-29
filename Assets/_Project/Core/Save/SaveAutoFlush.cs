using System;
using System.Threading;
using Core.Logging;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Core.Save
{
    internal sealed class SaveAutoFlush : IInitializable, IDisposable
    {
        private bool _isQuitFlushStarted;
        private bool _isQuitFlushCompleted;

        private readonly ISaveStore _saveStore;

        public SaveAutoFlush(ISaveStore saveStore)
        {
            _saveStore = saveStore;
        }

        public void Initialize()
        {
            Application.focusChanged += OnFocusChanged;
#if UNITY_EDITOR
            Application.quitting += OnQuitting;
#else
            Application.wantsToQuit += OnWantsToQuit;
#endif
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushAsync("focus loss", CancellationToken.None).Forget();
            }
        }

        private void OnQuitting()
        {
            FlushAsync("quit", CancellationToken.None).Forget();
        }

        private bool OnWantsToQuit()
        {
            if (_isQuitFlushCompleted)
            {
                return true;
            }

            if (!_isQuitFlushStarted)
            {
                _isQuitFlushStarted = true;
                FlushThenQuitAsync(CancellationToken.None).Forget();
            }

            return false;
        }

        private async UniTaskVoid FlushThenQuitAsync(CancellationToken ct)
        {
            await FlushAsync("quit", ct);
            _isQuitFlushCompleted = true;
            Application.Quit();
        }

        private async UniTask FlushAsync(string reason, CancellationToken ct)
        {
            var flushed = await _saveStore.FlushAsync(ct);

            if (flushed.TryPickT1(out var error, out _))
            {
                Log.Error(LogTags.Save, $"Flush on {reason} failed: {error.Message}");
            }
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
#if UNITY_EDITOR
            Application.quitting -= OnQuitting;
#else
            Application.wantsToQuit -= OnWantsToQuit;
#endif
        }
    }
}
