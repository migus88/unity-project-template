using AwesomeAssertions;
using Core.Environment;
using NUnit.Framework;

namespace Core.Tests.Environment
{
    public sealed class BuildEnvironmentValidationTests
    {
        private TestBuildEnvironments _environments = null!;

        [SetUp]
        public void SetUp()
        {
            _environments = new TestBuildEnvironments();
        }

        [TearDown]
        public void TearDown()
        {
            _environments.Dispose();
        }

        [Test]
        public void Validate_TelemetryDisallowedWithEmptyEndpoint_ReportsNothing()
        {
            // Arrange
            var environment = _environments.Create(isTelemetryAllowed: false, telemetryEndpoint: "");

            // Act
            var problems = BuildEnvironmentValidation.Validate(environment);

            // Assert
            problems.Should().BeEmpty();
        }

        [Test]
        public void Validate_TelemetryAllowedWithHttpsEndpoint_ReportsNothing()
        {
            // Arrange
            var environment = _environments.Create(isTelemetryAllowed: true, telemetryEndpoint: "https://telemetry.example.workers.dev");

            // Act
            var problems = BuildEnvironmentValidation.Validate(environment);

            // Assert
            problems.Should().BeEmpty();
        }

        [Test]
        public void Validate_EmptyName_ReportsName()
        {
            // Arrange
            var environment = _environments.Create(environmentName: " ");

            // Act
            var problems = BuildEnvironmentValidation.Validate(environment);

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("name");
        }

        [TestCase("")]
        [TestCase("http://telemetry.example.workers.dev")]
        [TestCase("telemetry.example.workers.dev/ingest")]
        public void Validate_TelemetryAllowedWithoutHttpsEndpoint_ReportsEndpoint(string endpoint)
        {
            // Arrange
            var environment = _environments.Create(isTelemetryAllowed: true, telemetryEndpoint: endpoint);

            // Act
            var problems = BuildEnvironmentValidation.Validate(environment);

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("endpoint");
        }

        [Test]
        public void Validate_RepeatedRemoteConfigKey_ReportsKey()
        {
            // Arrange
            var environment = _environments.Create("development", false, "", "battle_speed", "shop_size", "battle_speed");

            // Act
            var problems = BuildEnvironmentValidation.Validate(environment);

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("'battle_speed'");
        }
    }
}
