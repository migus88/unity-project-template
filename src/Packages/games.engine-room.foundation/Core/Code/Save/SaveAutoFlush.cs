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
        private IDisposable? _quitRegistration;

        private readonly ISaveStore _saveStore;
        private readonly IApplicationService _application;

        public SaveAutoFlush(ISaveStore saveStore, IApplicationService application)
        {
            _saveStore = saveStore;
            _application = application;
        }

        public void Initialize()
        {
            Application.focusChanged += OnFocusChanged;
            _quitRegistration = _application.AddQuitHandler(_ => FlushAsync("quit", CancellationToken.None));
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushAndForgetAsync("focus loss", CancellationToken.None).Forget();
            }
        }

        private async UniTaskVoid FlushAndForgetAsync(string reason, CancellationToken ct)
        {
            await FlushAsync(reason, ct);
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
            _quitRegistration?.Dispose();
        }
    }
}
