using AwesomeAssertions;
using Core.RemoteConfig;
using NUnit.Framework;

namespace Core.Tests.RemoteConfig
{
    public sealed class RemoteConfigUrlTests
    {
        [Test]
        public void Build_EndpointWithTrailingSlash_TrimsIt()
        {
            // Act
            var url = RemoteConfigUrl.Build("https://example.test/", "1.0", "OSXPlayer");

            // Assert
            url.Should().Be("https://example.test/v1/config?build=1.0&platform=OSXPlayer");
        }

        [Test]
        public void Build_VersionWithReservedCharacters_EscapesThem()
        {
            // Act
            var url = RemoteConfigUrl.Build("https://example.test", "1.0+dev build", "WindowsPlayer");

            // Assert
            url.Should().Be("https://example.test/v1/config?build=1.0%2Bdev%20build&platform=WindowsPlayer");
        }
    }
}
