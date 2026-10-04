using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
using Core.Settings;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using UnityEngine;
using UnityEngine.TestTools;
using Success = OneOf.Types.Success;

namespace Core.Tests
{
    public sealed class CoreStartupTests
    {
        private ISettingsLoader _settingsLoader = null!;
        private ISaveStore _saveStore = null!;
        private CoreStartup _startup = null!;

        [SetUp]
        public void SetUp()
        {
            _settingsLoader = Substitute.For<ISettingsLoader>();
            _settingsLoader.LoadAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            _saveStore = Substitute.For<ISaveStore>();
            _saveStore.SelectSlotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Success()));
            _startup = new CoreStartup(_settingsLoader, _saveStore);
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
