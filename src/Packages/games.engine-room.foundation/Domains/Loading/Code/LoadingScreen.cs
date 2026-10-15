using System;
using System.Threading;
using Core.Input;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Migs.MLock;
using Migs.MLock.Interfaces;
using R3;

namespace Loading
{
    public sealed class LoadingScreen : ILoadingScreen, IDisposable
    {
        private bool _isVisible;
        private bool _isDisposed;
        private int _fadeGeneration;
        private AsyncLazy _fade = UniTask.CompletedTask.ToAsyncLazy();
        private ILoadingScreenView? _view;

        private readonly LockSwitch<InputLockTag> _inputLock;
        private readonly CancellationTokenSource _lifetimeCts = new();
        private readonly UniTaskCompletionSource _viewAttached = new();

        public LoadingScreen(ILockService<InputLockTag> inputLocks)
        {
            _inputLock = inputLocks.CreateSwitchAll();
        }

        public UniTask ShowAsync(CancellationToken ct)
        {
            return SetVisibleAsync(true, ct);
        }

        public UniTask HideAsync(CancellationToken ct)
        {
            return SetVisibleAsync(false, ct);
        }

        public UniTask WaitForViewAsync(CancellationToken ct)
        {
            return _viewAttached.Task.AttachExternalCancellation(ct);
        }

        internal IDisposable Attach(ILoadingScreenView view)
        {
            _view = view;
            view.SetVisible(_isVisible);
            _viewAttached.TrySetResult();
            return Disposable.Create(() => Detach(view));
        }

        private async UniTask SetVisibleAsync(bool isVisible, CancellationToken ct)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isVisible != isVisible)
            {
                _isVisible = isVisible;

                if (isVisible)
                {
                    _inputLock.Set(true);
                }

                _fade = FadeAsync(isVisible, _lifetimeCts.Token).ToAsyncLazy();
            }

            await _fade.Task.AttachExternalCancellation(ct);
        }

        private async UniTask FadeAsync(bool isVisible, CancellationToken ct)
        {
            var generation = ++_fadeGeneration;

            if (_view != null)
            {
                await _view.FadeAsync(isVisible, ct).SuppressCancellationThrow();
            }

            if (generation == _fadeGeneration && !_isVisible)
            {
                _inputLock.Set(false);
            }
        }

        private void Detach(ILoadingScreenView view)
        {
            if (_view == view)
            {
                _view = null;
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
            _viewAttached.TrySetCanceled();
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            _inputLock.Dispose();
        }
    }
}
