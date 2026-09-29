using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
using Cysharp.Threading.Tasks;
using Gameplay.Progress;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;
using Success = OneOf.Types.Success;

namespace Gameplay.Tests.Progress
{
    public sealed class GameplayProgressServiceTests
    {
        private ISaveStore _saveStore = null!;
        private GameplayProgressService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _saveStore = Substitute.For<ISaveStore>();
            _service = new GameplayProgressService(_saveStore);
        }

        [Test]
        public void RecordRound_NoSavedProgress_WritesScoreAsNewBestScore()
        {
            // Arrange
            GivenSavedProgress(new NotFound());

            // Act
            var record = _service.RecordRound(30);

            // Assert
            record.Should().Be(new RoundRecord(BestScore: 30, IsNewBestScore: true));
            _saveStore.Received(1).Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 30, RoundsPlayed: 1));
        }

        [Test]
        public void RecordRound_LowerThanBestScore_KeepsBestScoreAndCountsRound()
        {
            // Arrange
            GivenSavedProgress(new GameplaySaveDto(BestScore: 50, RoundsPlayed: 2));

            // Act
            var record = _service.RecordRound(30);

            // Assert
            record.Should().Be(new RoundRecord(BestScore: 50, IsNewBestScore: false));
            _saveStore.Received(1).Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 50, RoundsPlayed: 3));
        }

        [Test]
        public void RecordRound_HigherThanBestScore_ReportsNewBestScore()
        {
            // Arrange
            GivenSavedProgress(new GameplaySaveDto(BestScore: 50, RoundsPlayed: 2));

            // Act
            var record = _service.RecordRound(60);

            // Assert
            record.Should().Be(new RoundRecord(BestScore: 60, IsNewBestScore: true));
            _saveStore.Received(1).Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 60, RoundsPlayed: 3));
        }

        [Test]
        public void RecordRound_EqualToBestScore_IsNotNewBestScore()
        {
            // Arrange
            GivenSavedProgress(new GameplaySaveDto(BestScore: 50, RoundsPlayed: 2));

            // Act
            var record = _service.RecordRound(50);

            // Assert
            record.IsNewBestScore.Should().BeFalse();
        }

        [Test]
        public void RecordRound_CorruptedProgress_StartsOver()
        {
            // Arrange
            GivenSavedProgress(new Corrupted("broken"));
            LogAssert.Expect(LogType.Warning, new Regex("corrupted"));

            // Act
            var record = _service.RecordRound(20);

            // Assert
            record.Should().Be(new RoundRecord(BestScore: 20, IsNewBestScore: true));
            _saveStore.Received(1).Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 20, RoundsPlayed: 1));
        }

        [Test]
        public void RecordRound_NegativeScore_Throws()
        {
            // Act
            Action act = () => _service.RecordRound(-1);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _saveStore.DidNotReceiveWithAnyArgs().Write(GameplaySave.Section, GameplaySaveDto.Empty);
        }

        [Test]
        public async Task SaveAsync_Always_FlushesSaveStore()
        {
            // Arrange
            _saveStore.FlushAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Success()));

            // Act
            var result = await _service.SaveAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _ = _saveStore.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        }

        private void GivenSavedProgress(OneOf<GameplaySaveDto, NotFound, Corrupted> progress)
        {
            _saveStore.Read(GameplaySave.Section).Returns(progress);
        }
    }
}
