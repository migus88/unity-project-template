using System;
using Core.Localization;
using Gameplay.Round;
using R3;
using VContainer.Unity;

namespace Gameplay.Hud
{
    internal sealed class HudPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly HudView _view;
        private readonly ScoreModel _score;
        private readonly RoundService _round;
        private readonly ILocalizationService _localization;

        public HudPresenter(HudView view, ScoreModel score, RoundService round, ILocalizationService localization)
        {
            _view = view;
            _score = score;
            _round = round;
            _localization = localization;
        }

        public void Start()
        {
            _score.Score
                .CombineLatest(_localization.Current, (score, _) => score)
                .Subscribe(score => _view.SetScore(_localization.Format(GameplayText.Score, score)))
                .AddTo(ref _subscriptions);
            _round.TimeLeft
                .CombineLatest(_localization.Current, (timeLeft, _) => timeLeft)
                .Subscribe(timeLeft => _view.SetTimeLeft(_localization.Format(GameplayText.TimeLeft, FormatTime(timeLeft))))
                .AddTo(ref _subscriptions);
        }

        private static string FormatTime(TimeSpan time)
        {
            var totalSeconds = (int)Math.Ceiling(time.TotalSeconds);
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
