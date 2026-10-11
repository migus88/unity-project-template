using System;
using AwesomeAssertions;
using Core.Telemetry;
using NUnit.Framework;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryBackoffTests
    {
        [TestCase(1, 15)]
        [TestCase(2, 30)]
        [TestCase(3, 60)]
        public void Delay_ConsecutiveFailures_Doubles(int attempt, int expectedSeconds)
        {
            // Act
            var delay = TelemetryBackoff.Delay(attempt, null);

            // Assert
            delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
        }

        [Test]
        public void Delay_ManyFailures_CapsAtMaxDelay()
        {
            // Act
            var delay = TelemetryBackoff.Delay(30, null);

            // Assert
            delay.Should().Be(TelemetryBackoff.MaxDelay);
        }

        [Test]
        public void Delay_LongerRetryAfter_UsesRetryAfter()
        {
            // Act
            var delay = TelemetryBackoff.Delay(1, TimeSpan.FromSeconds(120));

            // Assert
            delay.Should().Be(TimeSpan.FromSeconds(120));
        }
    }
}
