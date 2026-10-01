using AwesomeAssertions;
using Core.Editor.Packages;
using NUnit.Framework;

namespace Core.Tests.Packages
{
    public sealed class GitDependencyTests
    {
        [TestCase("https://github.com/owner/repo.git?path=src/Packages/name#1.0.0")]
        [TestCase("https://github.com/owner/repo.git")]
        [TestCase("git+https://example.com/owner/repo#main")]
        [TestCase("git+file:///Users/someone/repo?path=src/Packages/name#main")]
        [TestCase("git@github.com:owner/repo.git#1.0.0")]
        [TestCase("ssh://git@example.com/owner/repo.git")]
        public void IsGitUrl_GitEntry_ReturnsTrue(string entry)
        {
            // Act
            var isGitUrl = GitDependency.IsGitUrl(entry);

            // Assert
            isGitUrl.Should().BeTrue();
        }

        [TestCase("1.0.0")]
        [TestCase("file:../somewhere/name")]
        [TestCase("file:games.engine-room.foundation")]
        [TestCase("https://example.com/name-1.0.0.tgz")]
        [TestCase("")]
        [TestCase(null)]
        public void IsGitUrl_NonGitEntry_ReturnsFalse(string? entry)
        {
            // Act
            var isGitUrl = GitDependency.IsGitUrl(entry);

            // Assert
            isGitUrl.Should().BeFalse();
        }

        [TestCase("https://github.com/owner/repo.git?path=src/Packages/name#1.0.0", "1.0.0")]
        [TestCase("git+file:///repo?path=src/Packages/name#main", "main")]
        [TestCase("https://github.com/owner/repo.git#v2.1.0?path=src/Packages/name", "v2.1.0")]
        public void GetRevision_UrlWithRevision_ReturnsRevision(string url, string expected)
        {
            // Act
            var revision = GitDependency.GetRevision(url);

            // Assert
            revision.Should().Be(expected);
        }

        [Test]
        public void GetRevision_UrlWithoutRevision_ReturnsNull()
        {
            // Act
            var revision = GitDependency.GetRevision("https://github.com/owner/repo.git?path=src/Packages/name");

            // Assert
            revision.Should().BeNull();
        }

        [TestCase("1.2.3", "1.2.3", true)]
        [TestCase("v1.2.3", "1.2.3", true)]
        [TestCase("1.2.4", "1.2.3", false)]
        [TestCase("main", "1.2.3", false)]
        [TestCase(null, "1.2.3", false)]
        public void IsSameVersion_Revision_ComparesWithVersion(string? revision, string version, bool expected)
        {
            // Act
            var isSameVersion = GitDependency.IsSameVersion(revision, version);

            // Assert
            isSameVersion.Should().Be(expected);
        }

        [Test]
        public void FindEntry_ManifestWithPackage_ReturnsEntry()
        {
            // Arrange
            const string manifest = "{\"dependencies\":{\"other\":\"1.0.0\",\"name\":\"https://example.com/repo.git#1.0.0\"}}";

            // Act
            var entry = GitDependency.FindEntry(manifest, "name");

            // Assert
            entry.Should().Be("https://example.com/repo.git#1.0.0");
        }

        [Test]
        public void FindEntry_ManifestWithoutPackage_ReturnsNull()
        {
            // Arrange
            const string manifest = "{\"dependencies\":{\"other\":\"1.0.0\"}}";

            // Act
            var entry = GitDependency.FindEntry(manifest, "name");

            // Assert
            entry.Should().BeNull();
        }
    }
}
