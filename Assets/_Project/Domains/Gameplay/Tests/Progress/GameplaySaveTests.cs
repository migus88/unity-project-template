using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Gameplay.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TestUtils;

namespace Gameplay.Tests.Progress
{
    public sealed class GameplaySaveTests
    {
        private const string SlotPath = "Saves/slot_0.json";

        private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        private InMemoryFileStorage _disk = null!;

        [SetUp]
        public void SetUp()
        {
            _disk = new InMemoryFileStorage();
        }

        [Test]
        public void Migrate_FromVersion1_RenamesHighScoreAndAddsRoundsPlayed()
        {
            // Arrange
            var data = JObject.Parse("{\"highScore\":40}");

            // Act
            var result = GameplaySave.Migrate(data, 1);

            // Assert
            var migrated = result.Should().BeCase<JObject>().Which;
            JToken.DeepEquals(migrated, JObject.Parse("{\"bestScore\":40,\"roundsPlayed\":0}")).Should().BeTrue();
        }

        [TestCase("{}")]
        [TestCase("{\"highScore\":\"lots\"}")]
        [TestCase("{\"highScore\":99999999999}")]
        [TestCase("{\"highScore\":99999999999999999999999}")]
        public void Migrate_FromVersion1WithoutIntHighScore_ReturnsCorrupted(string json)
        {
            // Act
            var result = GameplaySave.Migrate(JObject.Parse(json), 1);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        [Test]
        public void Migrate_FromUnknownVersion_ReturnsCorrupted()
        {
            // Act
            var result = GameplaySave.Migrate(new JObject(), 0);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        [Test]
        public async Task Read_Version1Section_ReturnsMigratedProgress()
        {
            // Arrange
            _disk.Files[SlotPath] = "{\"formatVersion\":1,\"savedAtUtc\":\"2026-09-27T10:00:00Z\",\"sections\":{\"gameplay\":{\"version\":1,\"data\":{\"highScore\":40}}}}";
            var store = await CreateStoreAsync();

            // Act
            var result = store.Read(GameplaySave.Section);

            // Assert
            result.Should().BeCase<GameplaySaveDto>().Which.Should().Be(new GameplaySaveDto(40, 0));
        }

        [Test]
        public async Task Section_WrittenAndFlushed_NewStoreReadsSameProgress()
        {
            // Arrange
            var progress = new GameplaySaveDto(BestScore: 70, RoundsPlayed: 4);
            var store = await CreateStoreAsync();
            store.Write(GameplaySave.Section, progress);
            await store.FlushAsync(CancellationToken.None);

            // Act
            var reloaded = await CreateStoreAsync();
            var result = reloaded.Read(GameplaySave.Section);

            // Assert
            result.Should().BeCase<GameplaySaveDto>().Which.Should().Be(progress);
        }

        [Test]
        public async Task Section_WrittenAndFlushed_StoresCurrentVersionUnderGameplayKey()
        {
            // Arrange
            var store = await CreateStoreAsync();
            store.Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 70, RoundsPlayed: 4));

            // Act
            await store.FlushAsync(CancellationToken.None);

            // Assert
            var section = JObject.Parse(_disk.Files[SlotPath])["sections"]!["gameplay"]!;
            section["version"]!.Value<int>().Should().Be(GameplaySave.CurrentVersion);
            JToken.DeepEquals(section["data"], JObject.Parse("{\"bestScore\":70,\"roundsPlayed\":4}")).Should().BeTrue();
        }

        private async UniTask<SaveStore> CreateStoreAsync()
        {
            var store = new SaveStore(_disk, new JsonSerializer(), new FakeClock(Now));
            await store.SelectSlotAsync(0, CancellationToken.None);
            return store;
        }
    }
}
