using System.Text.RegularExpressions;
using AwesomeAssertions;
using Core.Privacy;
using Core.RemoteConfig;
using Core.Tests.Environment;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Privacy
{
    public sealed class PrivacyPolicyTests
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
        public void From_NoEnvironment_IsUnpublished()
        {
            // Act
            var policy = PrivacyPolicy.From(null);

            // Assert
            policy.Should().Be(new PrivacyPolicy(null, 1));
        }

        [Test]
        public void From_HttpsUrl_KeepsUrlAndVersion()
        {
            // Arrange
            var environment = _environments.CreateWithPrivacyPolicy("https://example.com/privacy", 3);

            // Act
            var policy = PrivacyPolicy.From(environment);

            // Assert
            policy.Should().Be(new PrivacyPolicy("https://example.com/privacy", 3));
        }

        [TestCase("http://example.com/privacy")]
        [TestCase("privacy.html")]
        public void From_NonHttpsUrl_DropsUrlAndWarns(string url)
        {
            // Arrange
            var environment = _environments.CreateWithPrivacyPolicy(url, 2);
            LogAssert.Expect(LogType.Warning, new Regex("privacy policy URL"));

            // Act
            var policy = PrivacyPolicy.From(environment);

            // Assert
            policy.Should().Be(new PrivacyPolicy(null, 2));
        }

        [Test]
        public void From_VersionBelowOne_RaisesToOne()
        {
            // Arrange
            var environment = _environments.CreateWithPrivacyPolicy(string.Empty, 0);

            // Act
            var policy = PrivacyPolicy.From(environment);

            // Assert
            policy.Version.Should().Be(1);
        }

        [TestCase(5, 5)]
        [TestCase(2, 3)]
        public void CurrentVersion_RemoteVersion_UsesTheHigherOfRemoteAndBuiltIn(int remoteVersion, int expected)
        {
            // Arrange
            var remoteConfig = Substitute.For<IRemoteConfigService>();
            remoteConfig.Get(CoreFlags.PrivacyPolicyVersion).Returns(remoteVersion);
            var policy = new PrivacyPolicy(null, 3);

            // Act
            var version = policy.CurrentVersion(remoteConfig);

            // Assert
            version.Should().Be(expected);
        }
    }
}
