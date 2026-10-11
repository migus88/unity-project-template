using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Privacy;
using Core.Results;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Privacy
{
    public sealed class ConsentServiceTests
    {
        private const string StoredInstallId = "stored-install-id";

        private FakeSettingsService _settings = null!;
        private ConsentService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = new FakeSettingsService();
            _service = new ConsentService(_settings);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public async Task LoadAsync_NoSection_IsUnknownWithoutInstallId()
        {
            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Unknown, null, null));
        }

        [TestCase(null, null, 1, true)]
        [TestCase("granted", 2, 2, false)]
        [TestCase("granted", 1, 2, true)]
        [TestCase("granted", 3, 2, false)]
        [TestCase("denied", 1, 2, false)]
        public async Task NeedsPrompt_StoredConsent_MatchesPolicy(string? consent, int? storedVersion, int currentVersion, bool expected)
        {
            // Arrange
            Store(consent, storedVersion, StoredInstallId);
            await _service.LoadAsync(CancellationToken.None);

            // Act
            var needsPrompt = _service.NeedsPrompt(currentVersion);

            // Assert
            needsPrompt.Should().Be(expected);
        }

        [Test]
        public async Task GrantAsync_FromUnknown_CreatesInstallIdAndSaves()
        {
            // Act
            await _service.GrantAsync(1, CancellationToken.None);

            // Assert
            _service.IsGranted.Should().BeTrue();
            _service.InstallId.Should().NotBeNullOrEmpty();
            _settings.Read(PrivacySettings.Section).Should().Be(new PrivacySettingsDto("granted", 1, _service.InstallId));
            _settings.SaveCount.Should().Be(1);
        }

        [Test]
        public async Task GrantAsync_AlreadyGranted_KeepsInstallId()
        {
            // Arrange
            await _service.GrantAsync(1, CancellationToken.None);
            var installId = _service.InstallId;

            // Act
            await _service.GrantAsync(2, CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Granted, 2, installId));
        }

        [Test]
        public async Task DenyAsync_AfterGrant_RegeneratesInstallId()
        {
            // Arrange
            await _service.GrantAsync(1, CancellationToken.None);
            var grantedId = _service.InstallId;

            // Act
            await _service.DenyAsync(1, CancellationToken.None);

            // Assert
            _service.IsGranted.Should().BeFalse();
            _service.InstallId.Should().NotBeNullOrEmpty().And.NotBe(grantedId);
        }

        [Test]
        public async Task DenyAsync_FromUnknown_LeavesInstallIdEmpty()
        {
            // Act
            await _service.DenyAsync(1, CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Denied, 1, null));
            _settings.Read(PrivacySettings.Section).Should().Be(new PrivacySettingsDto("denied", 1, null));
        }

        [Test]
        public async Task ClearAsync_AfterGrant_ReturnsToUnknown()
        {
            // Arrange
            await _service.GrantAsync(1, CancellationToken.None);

            // Act
            await _service.ClearAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Unknown, null, null));
            _settings.Read(PrivacySettings.Section).Should().Be(PrivacySettings.Default);
        }

        [Test]
        public async Task LoadAsync_StoredGranted_RestoresSnapshot()
        {
            // Arrange
            Store("granted", 3, StoredInstallId);

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Granted, 3, StoredInstallId));
            _settings.SaveCount.Should().Be(0);
        }

        [Test]
        public async Task LoadAsync_UnrecognisedConsent_IsUnknownAndWarns()
        {
            // Arrange
            Store("maybe", 1, StoredInstallId);
            LogAssert.Expect(LogType.Warning, "[Privacy] Stored consent 'maybe' is not recognised; treating it as unknown.");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Unknown, null, null));
        }

        [Test]
        public async Task LoadAsync_GrantedWithoutPolicyVersion_IsUnknown()
        {
            // Arrange
            Store("granted", null, StoredInstallId);
            LogAssert.Expect(LogType.Warning, "[Privacy] Stored consent has no valid policy version; asking again.");

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.Current.CurrentValue.State.Should().Be(ConsentState.Unknown);
        }

        [Test]
        public async Task LoadAsync_GrantedWithoutInstallId_CreatesAndWritesOne()
        {
            // Arrange
            Store("granted", 1, null);

            // Act
            await _service.LoadAsync(CancellationToken.None);

            // Assert
            _service.InstallId.Should().NotBeNullOrEmpty();
            _settings.Read(PrivacySettings.Section).InstallId.Should().Be(_service.InstallId);
            _settings.SaveCount.Should().Be(1);
        }

        [Test]
        public async Task GrantAsync_SaveFails_KeepsStateAndWarns()
        {
            // Arrange
            _settings.SaveResult = new Error("disk full");
            LogAssert.Expect(LogType.Warning, "[Privacy] Consent could not be saved, keeping it for this session: disk full");

            // Act
            await _service.GrantAsync(1, CancellationToken.None);

            // Assert
            _service.IsGranted.Should().BeTrue();
        }

        [Test]
        public void SessionId_TwoServices_Differ()
        {
            // Arrange
            using var other = new ConsentService(_settings);

            // Act
            var sessionId = other.SessionId;

            // Assert
            sessionId.Should().NotBeNullOrEmpty().And.NotBe(_service.SessionId);
        }

        private void Store(string? consent, int? policyVersion, string? installId)
        {
            _settings.Write(PrivacySettings.Section, new PrivacySettingsDto(consent, policyVersion, installId));
        }
    }
}
