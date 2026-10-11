using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Http;
using Core.Privacy;
using Core.RemoteConfig;
using Core.Results;
using Core.Save;
using Core.Settings;
using Core.Storage;
using Core.Tests.Privacy;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;
using Success = OneOf.Types.Success;

namespace Core.Tests
{
    public sealed class CoreStartupTests
    {
        private ISettingsLoader _settingsLoader = null!;
        private ISaveStore _saveStore = null!;
        private FakeSettingsService _settings = null!;
        private ConsentService _consent = null!;
        private FakeHttpService _http = null!;
        private RemoteConfigService _remoteConfig = null!;
        private CoreStartup _startup = null!;

        [SetUp]
        public void SetUp()
        {
            _settingsLoader = Substitute.For<ISettingsLoader>();
            _settingsLoader.LoadAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            _saveStore = Substitute.For<ISaveStore>();
            _saveStore.SelectSlotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Success()));
            _settings = new FakeSettingsService();
            _consent = new ConsentService(_settings);
            _http = new FakeHttpService();
            var source = new RemoteConfigSource("https://example.test", "test", Array.Empty<KeyValuePair<string, string>>(), "1.0", "OSXEditor");
            _remoteConfig = new RemoteConfigService(_http, new InMemoryFileStorage(), new JsonSerializer(), source);
            _http.Respond(_ => new HttpResult.HttpError(new HttpResponse(404, Array.Empty<byte>(), new Dictionary<string, string>())));
            _startup = new CoreStartup(_settingsLoader, _saveStore, _consent, _remoteConfig);
        }

        [TearDown]
        public void TearDown()
        {
            _consent.Dispose();
        }

        [Test]
        public async Task RunAsync_FirstRun_LoadsSettingsBeforeSelectingSaveSlot()
        {
            // Act
            await _startup.RunAsync(CancellationToken.None);

            // Assert
            Received.InOrder(() =>
            {
                _settingsLoader.LoadAsync(CancellationToken.None);
                _saveStore.SelectSlotAsync(0, CancellationToken.None);
            });
        }

        [Test]
        public async Task RunAsync_FirstRun_LoadsConsentAfterSettings()
        {
            // Arrange
            _settingsLoader.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                _settings.Write(PrivacySettings.Section, new PrivacySettingsDto("granted", 1, "install-id"));
                return UniTask.CompletedTask;
            });

            // Act
            await _startup.RunAsync(CancellationToken.None);

            // Assert
            _consent.IsGranted.Should().BeTrue();
        }

        [Test]
        public async Task RunAsync_FirstRun_LoadsRemoteConfig()
        {
            // Arrange
            var body = Encoding.UTF8.GetBytes("{\"schema\": 1, \"flags\": {\"telemetryEnabled\": false}}");
            _http.Enqueue(new HttpResult.Completed(new HttpResponse(200, body, new Dictionary<string, string>())));

            // Act
            await _startup.RunAsync(CancellationToken.None);

            // Assert
            _remoteConfig.Get(CoreFlags.TelemetryEnabled).Should().BeFalse();
        }

        [Test]
        public async Task RunAsync_SlotSelectionFails_LogsWarningAndCompletes()
        {
            // Arrange
            _saveStore.SelectSlotAsync(0, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Error("slot is corrupted")));
            LogAssert.Expect(LogType.Warning, "[Save] slot is corrupted");

            // Act
            Func<Task> act = () => _startup.RunAsync(CancellationToken.None).AsTask();

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task RunAsync_Twice_Throws()
        {
            // Arrange
            await _startup.RunAsync(CancellationToken.None);

            // Act
            Func<Task> act = () => _startup.RunAsync(CancellationToken.None).AsTask();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
