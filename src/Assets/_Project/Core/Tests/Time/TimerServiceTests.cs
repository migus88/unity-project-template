using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Time;
using NUnit.Framework;
using R3;
using TestUtils;

namespace Core.Tests.Time
{
    public sealed class TimerServiceTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 59, 500, DateTimeKind.Utc);

        private FakeClock _realClock = null!;
        private FakeClock _gameClock = null!;
        private TimerService _service = null!;
        private DisposableBag _subscriptions;

        [SetUp]
        public void SetUp()
        {
            _realClock = new FakeClock(Start);
            _gameClock = new FakeClock(Start);
            _service = new TimerService(_realClock, _gameClock);
            _subscriptions = default;
        }

        [TearDown]
        public void TearDown()
        {
            _subscriptions.Dispose();
            _service.Dispose();
        }

        [Test]
        public void Tick_WithinSameSecond_DoesNotEmit()
        {
            // Arrange
            var seconds = CountEmissions(_service.Real.EverySecond);
            _realClock.Advance(TimeSpan.FromMilliseconds(400));

            // Act
            _service.Tick();

            // Assert
            seconds().Should().Be(0);
        }

        [Test]
        public void Tick_CrossingSecondBoundary_EmitsOnce()
        {
            // Arrange
            var seconds = CountEmissions(_service.Real.EverySecond);
            _realClock.Advance(TimeSpan.FromMilliseconds(600));

            // Act
            _service.Tick();
            _service.Tick();

            // Assert
            seconds().Should().Be(1);
        }

        [Test]
        public void Tick_SeveralSecondsInOneFrame_EmitsOnce()
        {
            // Arrange
            var seconds = CountEmissions(_service.Real.EverySecond);
            _realClock.Advance(TimeSpan.FromSeconds(5));

            // Act
            _service.Tick();

            // Assert
            seconds().Should().Be(1);
        }

        [Test]
        public void Tick_CrossingMinuteBoundary_EmitsMinuteAndSecond()
        {
            // Arrange
            var seconds = CountEmissions(_service.Real.EverySecond);
            var minutes = CountEmissions(_service.Real.EveryMinute);
            _realClock.Advance(TimeSpan.FromMilliseconds(600));

            // Act
            _service.Tick();

            // Assert
            seconds().Should().Be(1);
            minutes().Should().Be(1);
        }

        [Test]
        public void Tick_CrossingSecondInsideMinute_DoesNotEmitMinute()
        {
            // Arrange
            _realClock.Advance(TimeSpan.FromMilliseconds(600));
            _service.Tick();
            var minutes = CountEmissions(_service.Real.EveryMinute);
            _realClock.Advance(TimeSpan.FromSeconds(1));

            // Act
            _service.Tick();

            // Assert
            minutes().Should().Be(0);
        }

        [Test]
        public void Tick_OnlyGameClockAdvanced_EmitsOnlyOnGame()
        {
            // Arrange
            var realSeconds = CountEmissions(_service.Real.EverySecond);
            var gameSeconds = CountEmissions(_service.Game.EverySecond);
            _gameClock.Advance(TimeSpan.FromSeconds(1));

            // Act
            _service.Tick();

            // Assert
            realSeconds().Should().Be(0);
            gameSeconds().Should().Be(1);
        }

        [Test]
        public void CountdownTo_FutureEnd_StartsWithRemainingTime()
        {
            // Act
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(10));

            // Assert
            countdown.CurrentValue.Should().Be(TimeSpan.FromSeconds(10));
        }

        [Test]
        public void CountdownTo_PastEnd_StartsAtZero()
        {
            // Act
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(-10));

            // Assert
            countdown.CurrentValue.Should().Be(TimeSpan.Zero);
        }

        [Test]
        public void CountdownTo_WithinSameSecond_KeepsValue()
        {
            // Arrange
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(10));
            _gameClock.Advance(TimeSpan.FromMilliseconds(400));

            // Act
            _service.Tick();

            // Assert
            countdown.CurrentValue.Should().Be(TimeSpan.FromSeconds(10));
        }

        [Test]
        public void CountdownTo_OnSecondTick_UpdatesBeforeSecondEmits()
        {
            // Arrange
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(10));
            var observed = new List<TimeSpan>();
            _service.Game.EverySecond.Subscribe(_ => observed.Add(countdown.CurrentValue)).AddTo(ref _subscriptions);
            _gameClock.Advance(TimeSpan.FromMilliseconds(600));

            // Act
            _service.Tick();

            // Assert
            observed.Should().Equal(TimeSpan.FromMilliseconds(9400));
        }

        [Test]
        public void CountdownTo_PastEnd_ClampsAtZero()
        {
            // Arrange
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(2));
            _gameClock.Advance(TimeSpan.FromSeconds(5));

            // Act
            _service.Tick();

            // Assert
            countdown.CurrentValue.Should().Be(TimeSpan.Zero);
        }

        [Test]
        public void CountdownTo_Disposed_IsNoLongerUpdated()
        {
            // Arrange
            var countdown = _service.Game.CountdownTo(Start.AddSeconds(10));
            countdown.Dispose();
            _gameClock.Advance(TimeSpan.FromSeconds(1));

            // Act
            Action act = () => _service.Tick();

            // Assert
            act.Should().NotThrow();
            countdown.CurrentValue.Should().Be(TimeSpan.FromSeconds(10));
        }

        private Func<int> CountEmissions(Observable<Unit> source)
        {
            var count = 0;
            source.Subscribe(_ => count++).AddTo(ref _subscriptions);
            return () => count;
        }
    }
}
