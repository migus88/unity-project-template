using System;
using System.Threading;
using Core.Time;
using Cysharp.Threading.Tasks;
using R3;

namespace Gameplay.Round
{
    internal sealed class RoundService : IDisposable
    {
        public bool IsRunning => _completion != null && !_isFinished;
        public ReadOnlyReactiveProperty<TimeSpan> TimeLeft => _timeLeft;

        private int _collectibleCount;
        private int _collectedCount;
        private bool _isFinished;
        private DateTime _startUtc;
        private DateTime _endUtc;
        private UniTaskCompletionSource<GameplayResult>? _completion;
        private DisposableBag _subscriptions;

        private readonly ScoreModel _score;
        private readonly ITimerService _timers;
        private readonly IGameClock _clock;
        private readonly ReactiveProperty<TimeSpan> _timeLeft = new(TimeSpan.Zero);

        public RoundService(ScoreModel score, ITimerService timers, IGameClock clock)
        {
            _score = score;
            _timers = timers;
            _clock = clock;
        }

        public async UniTask<GameplayResult> RunAsync(int collectibleCount, TimeSpan duration, CancellationToken ct)
        {
            if (_completion != null)
            {
                throw new InvalidOperationException("The round has already been started.");
            }

            if (collectibleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(collectibleCount), collectibleCount, "A round needs at least one collectible.");
            }

            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "A round needs a positive duration.");
            }

            ct.ThrowIfCancellationRequested();
            _collectibleCount = collectibleCount;
            _startUtc = _clock.UtcNow;
            _endUtc = _startUtc + duration;
            _completion = new UniTaskCompletionSource<GameplayResult>();

            var countdown = _timers.Game.CountdownTo(_endUtc);
            countdown.AddTo(ref _subscriptions);
            countdown.Subscribe(OnTimeLeftChanged).AddTo(ref _subscriptions);

            try
            {
                return await _completion.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isFinished = true;
                _subscriptions.Dispose();
            }
        }

        public void Collect(int points)
        {
            EnsureRunning();

            if (_clock.UtcNow >= _endUtc)
            {
                Finish(new GameplayResult.Lost(_score.Score.CurrentValue));
                return;
            }

            _score.Add(points);
            _collectedCount++;

            if (_collectedCount == _collectibleCount)
            {
                Finish(new GameplayResult.Won(_score.Score.CurrentValue, _clock.UtcNow - _startUtc));
            }
        }

        public void QuitToMenu()
        {
            EnsureRunning();
            Finish(new GameplayResult.QuitToMenu());
        }

        private void OnTimeLeftChanged(TimeSpan timeLeft)
        {
            _timeLeft.Value = timeLeft;

            if (timeLeft == TimeSpan.Zero && IsRunning)
            {
                Finish(new GameplayResult.Lost(_score.Score.CurrentValue));
            }
        }

        private void Finish(GameplayResult result)
        {
            _isFinished = true;
            _completion!.TrySetResult(result);
        }

        private void EnsureRunning()
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("The round is not running.");
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _timeLeft.Dispose();
        }
    }
}
