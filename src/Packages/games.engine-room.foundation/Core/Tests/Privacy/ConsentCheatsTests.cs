using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Cheats;
using Core.Privacy;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Privacy
{
    public sealed class ConsentCheatsTests
    {
        private FakeSettingsService _settings = null!;
        private ConsentService _consent = null!;
        private ICheat _cheat = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = new FakeSettingsService();
            _consent = new ConsentService(_settings);
            _cheat = new ConsentCheats(_consent).Cheats[0];
        }

        [TearDown]
        public void TearDown()
        {
            _consent.Dispose();
        }

        [Test]
        public async Task Show_Granted_ListsInstallAndSessionIds()
        {
            // Arrange
            await _consent.GrantAsync(2, CancellationToken.None);

            // Act
            var reply = await ExecuteAsync("consent show");

            // Assert
            reply.Should().Be($"state\tGranted\npolicy version\t2\ninstall id\t{_consent.InstallId}\nsession id\t{_consent.SessionId}");
        }

        [Test]
        public async Task Clear_AfterGrant_ReturnsToUnknown()
        {
            // Arrange
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            var reply = await ExecuteAsync("consent clear");

            // Assert
            reply.Should().Be("Consent cleared; the prompt shows on the next check.");
            _consent.Current.CurrentValue.State.Should().Be(ConsentState.Unknown);
        }

        [Test]
        public async Task Grant_WithoutVersion_UsesStoredVersion()
        {
            // Arrange
            await _consent.DenyAsync(3, CancellationToken.None);

            // Act
            var reply = await ExecuteAsync("consent grant");

            // Assert
            reply.Should().Be("Consent granted for policy version 3.");
            _consent.Current.CurrentValue.PolicyVersion.Should().Be(3);
        }

        private async Task<string> ExecuteAsync(string line)
        {
            var parsed = CheatArgumentParser.Parse(_cheat, CheatLine.Parse(line));
            var arguments = parsed.Should().BeCase<CheatParse.Parsed>().Which.Arguments;
            return await _cheat.ExecuteAsync(arguments, CancellationToken.None);
        }
    }
}
