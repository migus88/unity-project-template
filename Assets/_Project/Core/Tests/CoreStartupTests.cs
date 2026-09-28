using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
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
        private ISaveStore _saveStore = null!;
        private CoreStartup _startup = null!;

        [SetUp]
        public void SetUp()
        {
            _saveStore = Substitute.For<ISaveStore>();
            _saveStore.SelectSlotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Success()));
            _startup = new CoreStartup(_saveStore);
        }

        [Test]
        public async Task RunAsync_SelectsSaveSlotZero()
        {
            // Act
            await _startup.RunAsync(CancellationToken.None);

            // Assert
            _ = _saveStore.Received(1).SelectSlotAsync(0, CancellationToken.None);
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
