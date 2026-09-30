using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Settings;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Gameplay.UserSettings;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using R3;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;
using Success = OneOf.Types.Success;

namespace Gameplay.Tests.UserSettings
{
    public sealed class GameplaySettingsServiceTests
    {
        private ISettingsService _settings = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ISettingsService>();
            _settings.Read(GameplaySettings.Section).Returns(GameplaySettings.Default);
            _settings.SaveAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Success()));
        }

        [Test]
        public void CameraDistance_StoredValue_IsUsed()
        {
            // Arrange
            _settings.Read(GameplaySettings.Section).Returns(new GameplaySettingsDto(0.8f));

            // Act
            using var service = new GameplaySettingsService(_settings);

            // Assert
            service.CameraDistance.CurrentValue.Should().Be(0.8f);
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        public void CameraDistance_StoredValueOutOfRange_UsesDefaultAndWarns(float stored)
        {
            // Arrange
            _settings.Read(GameplaySettings.Section).Returns(new GameplaySettingsDto(stored));
            LogAssert.Expect(LogType.Warning, $"[Gameplay] Stored camera distance {stored} is out of range, using the default.");

            // Act
            using var service = new GameplaySettingsService(_settings);

            // Assert
            service.CameraDistance.CurrentValue.Should().Be(GameplaySettings.DefaultCameraDistance);
        }

        [Test]
        public void CameraDistance_StoredValueMissing_UsesDefaultAndWarns()
        {
            // Arrange
            var stored = new JsonSerializer().Deserialize<GameplaySettingsDto>("{}").Should().BeCase<GameplaySettingsDto>().Which;
            _settings.Read(GameplaySettings.Section).Returns(stored);
            LogAssert.Expect(LogType.Warning, "[Gameplay] Stored camera distance is missing, using the default.");

            // Act
            using var service = new GameplaySettingsService(_settings);

            // Assert
            stored.CameraDistance.Should().BeNull();
            service.CameraDistance.CurrentValue.Should().Be(GameplaySettings.DefaultCameraDistance);
        }

        [Test]
        public void SetCameraDistance_NewValue_UpdatesPropertyAndWritesSection()
        {
            // Arrange
            using var service = new GameplaySettingsService(_settings);
            var emitted = new List<float>();
            using var subscription = service.CameraDistance.Subscribe(emitted.Add);

            // Act
            service.SetCameraDistance(0.2f);

            // Assert
            emitted.Should().Equal(0.5f, 0.2f);
            _settings.Received(1).Write(GameplaySettings.Section, new GameplaySettingsDto(0.2f));
            _ = _settings.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void SetCameraDistance_OutOfRange_Throws(float cameraDistance)
        {
            // Arrange
            using var service = new GameplaySettingsService(_settings);

            // Act
            Action act = () => service.SetCameraDistance(cameraDistance);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _settings.DidNotReceiveWithAnyArgs().Write(default(SettingsSection<GameplaySettingsDto>)!, default!);
        }

        [Test]
        public async Task SaveAsync_NoChanges_DoesNotSave()
        {
            // Arrange
            using var service = new GameplaySettingsService(_settings);
            service.SetCameraDistance(GameplaySettings.DefaultCameraDistance);

            // Act
            var result = await service.SaveAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _ = _settings.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SaveAsync_AfterChange_SavesOnlyOnce()
        {
            // Arrange
            using var service = new GameplaySettingsService(_settings);
            service.SetCameraDistance(0.9f);

            // Act
            await service.SaveAsync(CancellationToken.None);
            await service.SaveAsync(CancellationToken.None);

            // Assert
            _ = _settings.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SaveAsync_Fails_ReturnsErrorAndRetriesNextTime()
        {
            // Arrange
            using var service = new GameplaySettingsService(_settings);
            service.SetCameraDistance(0.9f);
            _settings.SaveAsync(Arg.Any<CancellationToken>()).Returns(
                UniTask.FromResult<OneOf<Success, Error>>(new Error("read-only disk")),
                UniTask.FromResult<OneOf<Success, Error>>(new Success()));

            // Act
            var first = await service.SaveAsync(CancellationToken.None);
            var second = await service.SaveAsync(CancellationToken.None);

            // Assert
            first.Should().BeCase<Error>();
            second.Should().BeCase<Success>();
            _ = _settings.Received(2).SaveAsync(Arg.Any<CancellationToken>());
        }
    }
}
