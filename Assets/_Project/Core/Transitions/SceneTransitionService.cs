using System;
using System.Threading;
using Core.Domains;
using Core.Input;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;

namespace Core.Transitions
{
    public sealed class SceneTransitionService : ISceneTransitionService, IDisposable
    {
        private int _shownCount;
        private UniTask _fadeIn = UniTask.CompletedTask;
        private ILock<InputLockTag>? _inputLock;

        private readonly ITransitionOverlayView _overlay;
        private readonly ILockService<InputLockTag> _inputLocks;
        private readonly CancellationTokenSource _lifetime = new();

        public SceneTransitionService(ITransitionOverlayView overlay, ILockService<InputLockTag> inputLocks)
        {
            _overlay = overlay;
            _inputLocks = inputLocks;
        }

        public async UniTask ShowAsync(Transition transition, CancellationToken ct)
        {
            if (transition == Transition.None)
            {
                return;
            }

            _shownCount++;

            if (_shownCount == 1)
            {
                _inputLock ??= _inputLocks.LockAll();
                _fadeIn = _overlay.FadeInAsync(_lifetime.Token).Preserve();
            }

            await _fadeIn.AttachExternalCancellation(ct);
        }

        public async UniTask HideAsync(Transition transition, CancellationToken ct)
        {
            if (transition == Transition.None)
            {
                return;
            }

            if (_shownCount == 0)
            {
                throw new InvalidOperationException($"{nameof(HideAsync)}({transition}) was called without a matching {nameof(ShowAsync)}.");
            }

            _shownCount--;

            if (_shownCount > 0)
            {
                return;
            }

            await FadeOutAsync(_lifetime.Token).AttachExternalCancellation(ct);
        }

        private async UniTask FadeOutAsync(CancellationToken ct)
        {
            await _overlay.FadeOutAsync(ct);

            if (_shownCount == 0)
            {
                ReleaseInputLock();
            }
        }

        private void ReleaseInputLock()
        {
            _inputLock?.Dispose();
            _inputLock = null;
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            ReleaseInputLock();
        }
    }
}
