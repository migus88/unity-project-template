using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Time;
using Cysharp.Threading.Tasks;
using Gameplay.Round;
using NUnit.Framework;
using TestUtils;

namespace Gameplay.Tests.Round
{
    public sealed class RoundServiceTests
    {
        private const int Points = 10;

        private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

        private FakeClock _gameClock = null!;
        private TimerService _timers = null!;
        private ScoreModel _score = null!;
        private RoundService _round = null!;

        [SetUp]
        public void SetUp()
        {
            _gameClock = new FakeClock(Start);
            _timers = new TimerService(new FakeClock(Start), _gameClock);
            _score = new ScoreModel();
            _round = new RoundService(_score, _timers, _gameClock);
        }

        [TearDown]
        public void TearDown()
        {
            _round.Dispose();
            _score.Dispose();
            _timers.Dispose();
        }

        [Test]
        public void RunAsync_Started_IsRunningWithFullTimeLeft()
        {
            // Act
            _round.RunAsync(2, Duration, CancellationToken.None).Forget();

            // Assert
            _round.IsRunning.Should().BeTrue();
            _round.TimeLeft.CurrentValue.Should().Be(Duration);
        }

        [Test]
        public async Task RunAsync_AllCollected_ReturnsWonWithScoreAndElapsedTime()
        {
            // Arrange
            var run = _round.RunAsync(2, Duration, CancellationToken.None);
            _round.Collect(Points);
            _gameClock.Advance(TimeSpan.FromSeconds(12.5));

            // Act
            _round.Collect(Points);
            var result = await run;

            // Assert
            var won = result.Should().BeCase<GameplayResult.Won>().Which;
            won.Score.Should().Be(20);
            won.Time.Should().Be(TimeSpan.FromSeconds(12.5));
            _round.IsRunning.Should().BeFalse();
        }

        [Test]
        public async Task RunAsync_CountdownReachesZero_ReturnsLostWithScore()
        {
            // Arrange
            var run = _round.RunAsync(3, Duration, CancellationToken.None);
            _round.Collect(Points);
            _gameClock.Advance(Duration);

            // Act
            _timers.Tick();
            var result = await run;

            // Assert
            result.Should().BeCase<GameplayResult.Lost>().Which.Score.Should().Be(Points);
            _round.TimeLeft.CurrentValue.Should().Be(TimeSpan.Zero);
        }

        [Test]
        public void TimeLeft_OnSecondTick_UpdatesRemainingTime()
        {
            // Arrange
            _round.RunAsync(1, Duration, CancellationToken.None).Forget();
            _gameClock.Advance(TimeSpan.FromSeconds(3));

            // Act
            _timers.Tick();

            // Assert
            _round.TimeLeft.CurrentValue.Should().Be(TimeSpan.FromSeconds(27));
            _round.IsRunning.Should().BeTrue();
        }

        [Test]
        public async Task RunAsync_QuitToMenu_ReturnsQuitToMenu()
        {
            // Arrange
            var run = _round.RunAsync(2, Duration, CancellationToken.None);

            // Act
            _round.QuitToMenu();
            var result = await run;

            // Assert
            result.Should().BeCase<GameplayResult.QuitToMenu>();
        }

        [Test]
        public async Task Collect_AfterRoundEnded_Throws()
        {
            // Arrange
            var run = _round.RunAsync(1, Duration, CancellationToken.None);
            _round.Collect(Points);
            await run;

            // Act
            var act = () => _round.Collect(Points);

            // Assert
            act.Should().Throw<InvalidOperationException>();
            _score.Score.CurrentValue.Should().Be(Points);
        }

        [Test]
        public void Collect_BeforeRoundStarted_Throws()
        {
            // Act
            var act = () => _round.Collect(Points);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task RunAsync_SecondCall_Throws()
        {
            // Arrange
            _round.RunAsync(1, Duration, CancellationToken.None).Forget();

            // Act
            var act = async () => await _round.RunAsync(1, Duration, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task RunAsync_NoCollectibles_Throws(int collectibleCount)
        {
            // Act
            var act = async () => await _round.RunAsync(collectibleCount, Duration, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task RunAsync_Cancelled_ThrowsAndStopsRunning()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var run = _round.RunAsync(2, Duration, cts.Token);

            // Act
            cts.Cancel();
            var act = async () => await run;

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _round.IsRunning.Should().BeFalse();
        }
    }
}
