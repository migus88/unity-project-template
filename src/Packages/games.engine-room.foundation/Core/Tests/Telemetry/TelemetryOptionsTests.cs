using System.Text.RegularExpressions;
using AwesomeAssertions;
using Core.Telemetry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryOptionsTests
    {
        [TestCase(true, "https://telemetry.test", "0.1.0", true)]
        [TestCase(true, "https://telemetry.test", "1.2.3-beta.1", true)]
        [TestCase(false, "https://telemetry.test", "0.1.0", false)]
        [TestCase(true, " ", "0.1.0", false)]
        public void From_EnvironmentValues_AllowsOnlyWhenTelemetryAllowedWithEndpoint(bool isTelemetryAllowed, string endpoint, string build, bool expected)
        {
            // Act
            var options = TelemetryOptions.From(isTelemetryAllowed, endpoint, "key", "staging", build, "OSXPlayer");

            // Assert
            options.IsAllowed.Should().Be(expected);
        }

        [Test]
        public void From_BuildTheServerRejects_IsNotAllowedAndLogsError()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("build version 'dev'"));

            // Act
            var options = TelemetryOptions.From(true, "https://telemetry.test", "key", "staging", "dev", "OSXPlayer");

            // Assert
            options.IsAllowed.Should().BeFalse();
        }
    }
}
