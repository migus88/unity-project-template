using AwesomeAssertions;
using Core.Editor.Environment;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Environment
{
    public sealed class BuildEnvironmentSelectorTests
    {
        private const string MapPath = "Assets/Test/BuildEnvironmentMap.asset";

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

        [TestCase(true, "staging")]
        [TestCase(false, "production")]
        public void Select_MappedProfile_PicksEnvironmentForBuildKind(bool isDevelopment, string expected)
        {
            // Arrange
            var profile = _environments.CreateProfile("macOS");
            var mappings = new[]
            {
                new BuildEnvironmentMapping(_environments.CreateProfile("Windows"), _environments.Create("other"), _environments.Create("other")),
                new BuildEnvironmentMapping(profile, _environments.Create("staging"), _environments.Create("production")),
            };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, profile, isDevelopment, MapPath);

            // Assert
            selection.Should().BeCase<BuildEnvironmentSelection.Selected>().Which.Environment.EnvironmentName.Should().Be(expected);
        }

        [Test]
        public void Select_NoActiveProfile_AsksForBuildProfile()
        {
            // Arrange
            var mappings = new[] { new BuildEnvironmentMapping(_environments.CreateProfile("macOS"), _environments.Create(), _environments.Create()) };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, null, false, MapPath);

            // Assert
            selection.Should().BeCase<BuildEnvironmentSelection.NotSelected>().Which.Reason.Should().Contain("No build profile is active");
        }

        [Test]
        public void Select_UnmappedProfile_NamesProfileAndMap()
        {
            // Arrange
            var mappings = new[] { new BuildEnvironmentMapping(_environments.CreateProfile("macOS"), _environments.Create(), _environments.Create()) };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, _environments.CreateProfile("Windows"), false, MapPath);

            // Assert
            var reason = selection.Should().BeCase<BuildEnvironmentSelection.NotSelected>().Which.Reason;
            reason.Should().Contain("'Windows' has no build environment").And.Contain(MapPath);
        }

        [Test]
        public void Select_ProfileMappedTwice_ReportsDuplicate()
        {
            // Arrange
            var profile = _environments.CreateProfile("macOS");
            var mappings = new[]
            {
                new BuildEnvironmentMapping(profile, _environments.Create(), _environments.Create()),
                new BuildEnvironmentMapping(profile, _environments.Create(), _environments.Create()),
            };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, profile, false, MapPath);

            // Assert
            selection.Should().BeCase<BuildEnvironmentSelection.NotSelected>().Which.Reason.Should().Contain("more than once");
        }

        [Test]
        public void Select_EnvironmentMissingForBuildKind_ReportsBuildKind()
        {
            // Arrange
            var profile = _environments.CreateProfile("macOS");
            var mappings = new[] { new BuildEnvironmentMapping(profile, _environments.Create(), null) };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, profile, false, MapPath);

            // Assert
            selection.Should().BeCase<BuildEnvironmentSelection.NotSelected>().Which.Reason.Should().Contain("no release environment");
        }

        [Test]
        public void Select_InvalidEnvironment_ListsProblems()
        {
            // Arrange
            var profile = _environments.CreateProfile("macOS");
            var mappings = new[] { new BuildEnvironmentMapping(profile, _environments.Create(), _environments.Create("production", true, "")) };

            // Act
            var selection = BuildEnvironmentSelector.Select(mappings, profile, false, MapPath);

            // Assert
            selection.Should().BeCase<BuildEnvironmentSelection.NotSelected>().Which.Reason.Should().Contain("is invalid").And.Contain("endpoint");
        }
    }
}
