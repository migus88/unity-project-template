using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Audio;
using Core.Input;
using Core.Localization;
using Core.Privacy;
using Core.Results;
using Core.Settings;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Privacy
{
    public sealed class PrivacySettingsTests
    {
        private static readonly SettingsDefaults Defaults = new(1f, 1f, 1f, 1f, Language.English);

        private InMemoryFileStorage _disk = null!;
        private GameInput _actions = null!;
        private IInputService _input = null!;
        private IGraphicsDevice _graphics = null!;

        [SetUp]
        public void SetUp()
        {
            _disk = new InMemoryFileStorage();
            _actions = new GameInput();
            _input = Substitute.For<IInputService>();
            _input.Actions.Returns(_actions);
            _graphics = Substitute.For<IGraphicsDevice>();
            _graphics.QualityLevel.Returns(0);
            _graphics.QualityLevelCount.Returns(1);
            _graphics.FullScreenMode.Returns(FullScreenMode.Windowed);
            _graphics.Resolution.Returns(new Resolution { width = 1280, height = 720, refreshRateRatio = new RefreshRate { numerator = 60, denominator = 1 } });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_actions.asset);
        }

        [Test]
        public async Task RoundTrip_RealSettingsService_RestoresConsent()
        {
            // Arrange
            string? installId;

            using (var settings = CreateSettingsService())
            using (var consent = new ConsentService(settings))
            {
                await settings.LoadAsync(CancellationToken.None);
                await consent.LoadAsync(CancellationToken.None);
                await consent.GrantAsync(2, CancellationToken.None);
                installId = consent.InstallId;
            }

            using var reloadedSettings = CreateSettingsService();
            using var reloaded = new ConsentService(reloadedSettings);
            await reloadedSettings.LoadAsync(CancellationToken.None);

            // Act
            await reloaded.LoadAsync(CancellationToken.None);

            // Assert
            reloaded.Current.CurrentValue.Should().Be(new ConsentSnapshot(ConsentState.Granted, 2, installId));
            var section = JObject.Parse(_disk.Files["settings.json"])["sections"]!["privacy"]!;
            section["version"]!.Value<int>().Should().Be(PrivacySettings.CurrentVersion);
            section["data"]!["consent"]!.Value<string>().Should().Be("granted");
        }

        [Test]
        public void Migrate_UnsupportedVersion_ReturnsCorrupted()
        {
            // Act
            var migrated = PrivacySettings.Migrate(new JObject(), 0);

            // Assert
            migrated.Should().BeCase<Corrupted>().Which.Reason.Should().Be("Privacy settings have no migration from version 0.");
        }

        private SettingsService CreateSettingsService()
        {
            return new SettingsService(
                _disk,
                new Core.Storage.JsonSerializer(),
                Substitute.For<IAudioService>(),
                Substitute.For<ILocalizationService>(),
                _input,
                _graphics,
                Defaults,
                [Language.English]);
        }
    }
}
