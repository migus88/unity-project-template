using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Time;
using NUnit.Framework;
using R3;

namespace Core.Tests.Time
{
    public sealed class TimeServiceTests
    {
        private TimeService _service = null!;

        [SetUp]
        public void SetUp()
        {
            UnityEngine.Time.timeScale = 1f;
            _service = new TimeService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            UnityEngine.Time.timeScale = 1f;
        }

        [Test]
        public void IsPaused_NothingPaused_IsFalse()
        {
            // Act
            var isPaused = _service.IsPaused.CurrentValue;

            // Assert
            isPaused.Should().BeFalse();
        }

        [Test]
        public void Pause_Called_PausesAndStopsUnityTime()
        {
            // Act
            _service.Pause();

            // Assert
            _service.IsPaused.CurrentValue.Should().BeTrue();
            UnityEngine.Time.timeScale.Should().Be(0f);
        }

        [Test]
        public void DisposeHandle_OtherHandleAlive_StaysPaused()
        {
            // Arrange
            var first = _service.Pause();
            _service.Pause();

            // Act
            first.Dispose();

            // Assert
            _service.IsPaused.CurrentValue.Should().BeTrue();
            UnityEngine.Time.timeScale.Should().Be(0f);
        }

        [Test]
        public void DisposeHandle_LastHandle_ResumesWithTimeScale()
        {
            // Arrange
            _service.TimeScale = 0.5f;
            var first = _service.Pause();
            var second = _service.Pause();
            first.Dispose();

            // Act
            second.Dispose();

            // Assert
            _service.IsPaused.CurrentValue.Should().BeFalse();
            UnityEngine.Time.timeScale.Should().Be(0.5f);
        }

        [Test]
        public void DisposeHandle_Twice_SecondCallIsIgnored()
        {
            // Arrange
            var first = _service.Pause();
            _service.Pause();
            first.Dispose();

            // Act
            first.Dispose();

            // Assert
            _service.IsPaused.CurrentValue.Should().BeTrue();
        }

        [Test]
        public void TimeScale_SetWhileRunning_AppliesToUnityTime()
        {
            // Act
            _service.TimeScale = 2f;

            // Assert
            _service.TimeScale.Should().Be(2f);
            UnityEngine.Time.timeScale.Should().Be(2f);
        }

        [Test]
        public void TimeScale_SetWhilePaused_KeepsUnityTimeStoppedUntilResumed()
        {
            // Arrange
            var pause = _service.Pause();

            // Act
            _service.TimeScale = 2f;
            var timeScaleWhilePaused = UnityEngine.Time.timeScale;
            pause.Dispose();

            // Assert
            timeScaleWhilePaused.Should().Be(0f);
            UnityEngine.Time.timeScale.Should().Be(2f);
        }

        [Test]
        public void TimeScale_Negative_Throws()
        {
            // Act
            Action act = () => _service.TimeScale = -1f;

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void IsPaused_PauseAndResume_NotifiesSubscribers()
        {
            // Arrange
            var values = new List<bool>();
            using var subscription = _service.IsPaused.Subscribe(values.Add);

            // Act
            _service.Pause().Dispose();

            // Assert
            values.Should().Equal(false, true, false);
        }
    }
}
