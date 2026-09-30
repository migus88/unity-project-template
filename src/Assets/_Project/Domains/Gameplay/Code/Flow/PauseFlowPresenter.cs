using System;
using System.Threading;
using Core.Domains;
using Core.Input;
using Core.Logging;
using Core.Time;
using Cysharp.Threading.Tasks;
using Gameplay.Pause;
using Gameplay.Round;
using Gameplay.UserSettings;
using Migs.MLock.Interfaces;
using R3;
using Settings;
using VContainer.Unity;

namespace Gameplay.Flow
{
    internal sealed class PauseFlowPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly PauseRequests _requests;
        private readonly RoundService _round;
        private readonly ITimeService _time;
        private readonly ILockService<InputLockTag> _locks;
        private readonly PauseDomain _pauseDomain;
        private readonly SettingsDomain _settingsDomain;
        private readonly GameplaySettingsService _gameplaySettings;

        public PauseFlowPresenter(PauseRequests requests, RoundService round, ITimeService time, ILockService<InputLockTag> locks, PauseDomain pauseDomain, SettingsDomain settingsDomain, GameplaySettingsService gameplaySettings)
        {
            _requests = requests;
            _round = round;
            _time = time;
            _locks = locks;
            _pauseDomain = pauseDomain;
            _settingsDomain = settingsDomain;
            _gameplaySettings = gameplaySettings;
        }

        public void Start()
        {
            _requests.Requested
                .Where(_ => _round.IsRunning)
                .SubscribeAwait((_, ct) => PauseAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private async UniTask PauseAsync(CancellationToken ct)
        {
            using var timePause = _time.Pause();
            using var inputLock = _locks.Lock(InputLockTag.Movement);
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            if (!_round.IsRunning)
            {
                return;
            }

            while (true)
            {
                var result = await _pauseDomain.RunAsync(new PauseArgs(), Transition.None, ct);
                await SaveGameplaySettingsAsync(ct);

                if (result.TryPickT1(out _, out var resumeOrQuit))
                {
                    await _settingsDomain.RunAsync(new SettingsArgs(), Transition.None, ct);
                    continue;
                }

                resumeOrQuit.Switch(
                    resume => { },
                    quitToMenu => QuitToMenu());
                return;
            }
        }

        private async UniTask SaveGameplaySettingsAsync(CancellationToken ct)
        {
            var saved = await _gameplaySettings.SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Gameplay, $"Gameplay settings could not be saved: {error.Message}");
            }
        }

        private void QuitToMenu()
        {
            if (_round.IsRunning)
            {
                _round.QuitToMenu();
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
