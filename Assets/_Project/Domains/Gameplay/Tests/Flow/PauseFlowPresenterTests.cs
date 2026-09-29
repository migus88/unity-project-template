using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Content;
using Core.Domains;
using Core.Input;
using Core.Time;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Gameplay.Flow;
using Gameplay.Pause;
using Gameplay.Round;
using Migs.MLock.Interfaces;
using NSubstitute;
using NUnit.Framework;
using Settings;
using TestUtils;
using UnityEngine;

namespace Gameplay.Tests.Flow
{
    public sealed class PauseFlowPresenterTests
    {
        private const int Points = 10;

        private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

        private FakeClock _gameClock = null!;
        private TimerService _timers = null!;
        private ScoreModel _score = null!;
        private RoundService _round = null!;
        private PauseRequests _requests = null!;
        private IDisposable _timePause = null!;
        private ITimeService _time = null!;
        private ISceneTransitionService _transitions = null!;
        private DomainRunner _runner = null!;
        private PauseDomainDescriptor _pauseDescriptor = null!;
        private SettingsDomainDescriptor _settingsDescriptor = null!;
        private PauseFlowPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _gameClock = new FakeClock(Start);
            _timers = new TimerService(new FakeClock(Start), _gameClock);
            _score = new ScoreModel();
            _round = new RoundService(_score, _timers, _gameClock);
            _requests = new PauseRequests();
            _timePause = Substitute.For<IDisposable>();
            _time = Substitute.For<ITimeService>();
            _time.Pause().Returns(_timePause);
            _transitions = Substitute.For<ISceneTransitionService>();
            _transitions.ShowAsync(Arg.Any<Transition>(), Arg.Any<CancellationToken>())
                .Returns(call => UniTask.Never(call.ArgAt<CancellationToken>(1)));
            _runner = new DomainRunner(_transitions, Substitute.For<IContentDirectoryRegistry>(), Substitute.For<ISceneLoader>());
            _pauseDescriptor = ScriptableObject.CreateInstance<PauseDomainDescriptor>();
            _settingsDescriptor = ScriptableObject.CreateInstance<SettingsDomainDescriptor>();
            var gameplayScope = new ScopeRef(null!, 1);
            _presenter = new PauseFlowPresenter(
                _requests,
                _round,
                _time,
                Substitute.For<ILockService<InputLockTag>>(),
                new PauseDomain(_runner, gameplayScope, _pauseDescriptor),
                new SettingsDomain(_runner, gameplayScope, _settingsDescriptor));
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _runner.Dispose();
            _requests.Dispose();
            _round.Dispose();
            _score.Dispose();
            _timers.Dispose();
            UnityEngine.Object.DestroyImmediate(_pauseDescriptor);
            UnityEngine.Object.DestroyImmediate(_settingsDescriptor);
        }

        [Test]
        public async Task PauseRequested_RoundRunningAtEndOfFrame_RunsPauseDomain()
        {
            // Arrange
            _round.RunAsync(1, Duration, CancellationToken.None).Forget();
            _presenter.Start();

            // Act
            _requests.Request();
            await UniTask.DelayFrame(2);

            // Assert
            _ = _transitions.Received(1).ShowAsync(Transition.None, Arg.Any<CancellationToken>());
            _timePause.DidNotReceive().Dispose();
        }

        [Test]
        public async Task PauseRequested_RoundEndsInSameFrame_DoesNotRunPauseDomainAndReleasesPause()
        {
            // Arrange
            _round.RunAsync(1, Duration, CancellationToken.None).Forget();
            _presenter.Start();

            // Act
            _requests.Request();
            _round.Collect(Points);
            await UniTask.DelayFrame(2);

            // Assert
            _ = _transitions.DidNotReceiveWithAnyArgs().ShowAsync(default, default);
            _timePause.Received(1).Dispose();
        }
    }
}
