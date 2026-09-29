using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Transitions
{
    public sealed class TransitionOverlayView : MonoBehaviour, ITransitionOverlayView
    {
        [SerializeField, Required] private CanvasGroup _canvasGroup = null!;
        [SerializeField, MinValue(0)] private float _fadeSeconds = 0.3f;

        private int _fadeVersion;

        public UniTask FadeInAsync(CancellationToken ct)
        {
            _canvasGroup.blocksRaycasts = true;
            return FadeToAsync(1f, ct);
        }

        public async UniTask FadeOutAsync(CancellationToken ct)
        {
            var isCompleted = await FadeToAsync(0f, ct);

            if (isCompleted)
            {
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private async UniTask<bool> FadeToAsync(float targetAlpha, CancellationToken ct)
        {
            var version = ++_fadeVersion;

            if (_fadeSeconds <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                return true;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            var alphaPerSecond = 1f / _fadeSeconds;

            while (_canvasGroup.alpha != targetAlpha)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);

                if (version != _fadeVersion)
                {
                    return false;
                }

                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, alphaPerSecond * UnityEngine.Time.unscaledDeltaTime);
            }

            return version == _fadeVersion;
        }
    }
}
