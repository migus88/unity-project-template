using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Content;
using Core.Domains;
using Core.Input;
using Core.Results;
using Core.Settings;
using Core.Time;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Gameplay.Flow;
using Gameplay.Pause;
using Gameplay.Round;
using Gameplay.UserSettings;
using Migs.MLock.Interfaces;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using Settings;
using TestUtils;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private IContentDirectoryRegistry _contentDirectories = null!;
        private CancellationTokenSource _sceneLoadCts = null!;
        private DomainRunner _runner = null!;
        private PauseDomainDescriptor _pauseDescriptor = null!;
        private PauseContent _pauseContent = null!;
        private SettingsDomainDescriptor _settingsDescriptor = null!;
        private GameplaySettingsService _gameplaySettings = null!;
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
            _pauseDescriptor = ScriptableObject.CreateInstance<PauseDomainDescriptor>();
            _pauseContent = ScriptableObject.CreateInstance<PauseContent>();
            _contentDirectories = Substitute.For<IContentDirectoryRegistry>();
            _contentDirectories.GetContent(Arg.Any<DomainDescriptor>()).Returns((OneOf<DomainContent, NotFound>)_pauseContent);
            _sceneLoadCts = new CancellationTokenSource();
            var sceneLoader = Substitute.For<ISceneLoader>();
            sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.Never<OneOf<Scene, NotFound>>(_sceneLoadCts.Token));
            _runner = new DomainRunner(Substitute.For<ILoadingScreen>(), _contentDirectories, sceneLoader);
            _settingsDescriptor = ScriptableObject.CreateInstance<SettingsDomainDescriptor>();
            var settings = Substitute.For<ISettingsService>();
            settings.Read(GameplaySettings.Section).Returns(GameplaySettings.Default);
            _gameplaySettings = new GameplaySettingsService(settings);
            var gameplayScope = new ScopeRef(null!, 1);
            _presenter = new PauseFlowPresenter(
                _requests,
                _round,
                _time,
                Substitute.For<ILockService<InputLockTag>>(),
                new PauseDomain(_runner, gameplayScope, _pauseDescriptor),
                new SettingsDomain(_runner, gameplayScope, _settingsDescriptor),
                _gameplaySettings);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _sceneLoadCts.Cancel();
            _sceneLoadCts.Dispose();
            _gameplaySettings.Dispose();
            _runner.Dispose();
            _requests.Dispose();
            _round.Dispose();
            _score.Dispose();
            _timers.Dispose();
            UnityEngine.Object.DestroyImmediate(_pauseDescriptor);
            UnityEngine.Object.DestroyImmediate(_pauseContent);
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
            _contentDirectories.Received(1).GetContent(_pauseDescriptor);
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
            _contentDirectories.DidNotReceiveWithAnyArgs().GetContent(default!);
            _timePause.Received(1).Dispose();
        }
    }
}
