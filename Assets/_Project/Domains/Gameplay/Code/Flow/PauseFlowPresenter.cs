using System;
using System.Threading;
using Core.Domains;
using Core.Input;
using Core.Time;
using Cysharp.Threading.Tasks;
using Gameplay.Pause;
using Gameplay.Round;
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

        public PauseFlowPresenter(PauseRequests requests, RoundService round, ITimeService time, ILockService<InputLockTag> locks, PauseDomain pauseDomain, SettingsDomain settingsDomain)
        {
            _requests = requests;
            _round = round;
            _time = time;
            _locks = locks;
            _pauseDomain = pauseDomain;
            _settingsDomain = settingsDomain;
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
