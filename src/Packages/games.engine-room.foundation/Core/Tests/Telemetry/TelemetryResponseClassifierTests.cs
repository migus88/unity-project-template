using System;
using AwesomeAssertions;
using Core.Http;
using Core.Telemetry;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryResponseClassifierTests
    {
        [Test]
        public void Classify_Accepted_Sent()
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(TelemetryTestData.Status(202));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Sent>();
        }

        [TestCase(400)]
        [TestCase(401)]
        [TestCase(403)]
        public void Classify_ClientError_Rejected(long status)
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(TelemetryTestData.Status(status));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Rejected>().Which.Status.Should().Be(status);
        }

        [Test]
        public void Classify_TooManyRequestsWithRetryAfter_RetryAfterThatDelay()
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(TelemetryTestData.Status(429, "30"));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Retry>().Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
        }

        [Test]
        public void Classify_ServerError_Retry()
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(TelemetryTestData.Status(503));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Retry>().Which.RetryAfter.Should().BeNull();
        }

        [Test]
        public void Classify_NetworkError_Retry()
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(new HttpResult.NetworkError("offline"));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Retry>();
        }

        [Test]
        public void Classify_Timeout_Retry()
        {
            // Act
            var outcome = TelemetryResponseClassifier.Classify(new HttpResult.Timeout(TimeSpan.FromSeconds(10)));

            // Assert
            outcome.Should().BeCase<TelemetrySendOutcome.Retry>();
        }
    }
}
