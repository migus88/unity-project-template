using System;
using System.Threading;
using Core.Input;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;
using R3;

namespace Loading
{
    public sealed class LoadingScreen : ILoadingScreen, IDisposable
    {
        private bool _isVisible;
        private AsyncLazy _fade = UniTask.CompletedTask.ToAsyncLazy();
        private ILoadingScreenView? _view;
        private ILock<InputLockTag>? _inputLock;

        private readonly ILockService<InputLockTag> _inputLocks;
        private readonly CancellationTokenSource _lifetimeCts = new();

        public LoadingScreen(ILockService<InputLockTag> inputLocks)
        {
            _inputLocks = inputLocks;
        }

        public UniTask ShowAsync(CancellationToken ct)
        {
            return SetVisibleAsync(true, ct);
        }

        public UniTask HideAsync(CancellationToken ct)
        {
            return SetVisibleAsync(false, ct);
        }

        internal IDisposable Attach(ILoadingScreenView view)
        {
            _view = view;
            view.SetVisible(_isVisible);
            return Disposable.Create(() => Detach(view));
        }

        private async UniTask SetVisibleAsync(bool isVisible, CancellationToken ct)
        {
            if (_isVisible != isVisible)
            {
                _isVisible = isVisible;

                if (isVisible)
                {
                    _inputLock ??= _inputLocks.LockAll();
                }

                _fade = FadeAsync(isVisible, _lifetimeCts.Token).ToAsyncLazy();
            }

            await _fade.Task.AttachExternalCancellation(ct);
        }

        private async UniTask FadeAsync(bool isVisible, CancellationToken ct)
        {
            if (_view != null)
            {
                await _view.FadeAsync(isVisible, ct).SuppressCancellationThrow();
            }

            if (!_isVisible)
            {
                ReleaseInputLock();
            }
        }

        private void Detach(ILoadingScreenView view)
        {
            if (_view == view)
            {
                _view = null;
            }
        }

        private void ReleaseInputLock()
        {
            _inputLock?.Dispose();
            _inputLock = null;
        }

        public void Dispose()
        {
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            ReleaseInputLock();
        }
    }
}
