using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Loading
{
    internal sealed class LoadingScreenView : MonoBehaviour, ILoadingScreenView
    {
        private const float MaxFadeStepSeconds = 1f / 30f;

        [SerializeField] private Canvas _canvas = null!;
        [SerializeField] private CanvasGroup _canvasGroup = null!;
        [SerializeField] private Animator _spinner = null!;
        [SerializeField, Min(0)] private float _fadeSeconds = 0.3f;

        private int _fadeVersion;

        public void SetVisible(bool isVisible)
        {
            _fadeVersion++;
            _canvasGroup.alpha = isVisible ? 1f : 0f;
            SetShown(isVisible);
        }

        public async UniTask FadeAsync(bool isVisible, CancellationToken ct)
        {
            if (_fadeSeconds <= 0f)
            {
                SetVisible(isVisible);
                return;
            }

            var version = ++_fadeVersion;
            var targetAlpha = isVisible ? 1f : 0f;

            if (isVisible)
            {
                SetShown(true);
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            var alphaPerSecond = 1f / _fadeSeconds;

            while (_canvasGroup.alpha != targetAlpha)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);

                if (version != _fadeVersion)
                {
                    return;
                }

                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, alphaPerSecond * Mathf.Min(Time.unscaledDeltaTime, MaxFadeStepSeconds));
            }

            if (!isVisible)
            {
                SetShown(false);
            }
        }

        private void SetShown(bool isShown)
        {
            _canvas.enabled = isShown;
            _canvasGroup.blocksRaycasts = isShown;
            _spinner.enabled = isShown;
        }
    }
}
