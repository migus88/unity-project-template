using System;
using AwesomeAssertions;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Testing
{
    public sealed class TestOpenLedgerTests
    {
        private const string ScenePath = "Assets/Scenes/A.unity";
        private const string OtherPath = "Assets/Scenes/B.unity";

        private readonly object _fixture = new();
        private TestOpenLedger _ledger = null!;

        [SetUp]
        public void SetUp()
        {
            _ledger = new TestOpenLedger();
        }

        [Test]
        public void Record_SamePathTwiceInOneFixture_FailsNamingPathAndFixture()
        {
            // Arrange
            _ledger.Record(InFixture(_fixture), ScenePath);

            // Act
            Action act = () => _ledger.Record(InFixture(_fixture), ScenePath);

            // Assert
            act.Should().Throw<AssertionException>().WithMessage($"*{ScenePath}*Probe.Fixture*OneTimeSetUp*");
        }

        [Test]
        public void Record_SamePathInAnotherFixture_Passes()
        {
            // Arrange
            _ledger.Record(InFixture(_fixture), ScenePath);

            // Act
            Action act = () => _ledger.Record(InFixture(new object()), ScenePath);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Record_OtherPathInOneFixture_Passes()
        {
            // Arrange
            _ledger.Record(InFixture(_fixture), ScenePath);

            // Act
            Action act = () => _ledger.Record(InFixture(_fixture), OtherPath);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Record_OutsideAnyFixture_NeverFails()
        {
            // Arrange
            _ledger.Record(TestOpenContext.None, ScenePath);

            // Act
            Action act = () => _ledger.Record(TestOpenContext.None, ScenePath);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Record_FixtureReturnsAfterAnother_StartsFresh()
        {
            // Arrange
            _ledger.Record(InFixture(_fixture), ScenePath);
            _ledger.Record(InFixture(new object()), OtherPath);

            // Act
            Action act = () => _ledger.Record(InFixture(_fixture), ScenePath);

            // Assert
            act.Should().NotThrow();
        }

        private static TestOpenContext InFixture(object fixture)
        {
            return new TestOpenContext(fixture, "Probe.Fixture");
        }
    }
}
