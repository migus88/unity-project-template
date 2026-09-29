using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Gameplay.Progress;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using Success = OneOf.Types.Success;

namespace Gameplay.Tests.Progress
{
    public sealed class GameplaySaveTests
    {
        private const string SlotPath = "Saves/slot_0.json";

        private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        private Dictionary<string, string> _files = null!;
        private IFileStorage _storage = null!;

        [SetUp]
        public void SetUp()
        {
            _files = new Dictionary<string, string>();
            _storage = Substitute.For<IFileStorage>();
            _storage.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => ReadFile(call.Arg<string>()));
            _storage.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => WriteFile(call.ArgAt<string>(0), call.ArgAt<string>(1)));
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
        public void Migrate_FromVersion1WithoutIntegerHighScore_ReturnsCorrupted(string json)
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
            _files[SlotPath] = "{\"formatVersion\":1,\"savedAtUtc\":\"2026-09-27T10:00:00Z\",\"sections\":{\"gameplay\":{\"version\":1,\"data\":{\"highScore\":40}}}}";
            var store = await CreateStoreAsync();

            // Act
            var result = store.Read(GameplaySave.Section);

            // Assert
            result.Should().BeCase<GameplaySaveDto>().Which.Should().Be(new GameplaySaveDto(40, 0));
        }

        [Test]
        public async Task WriteThenFlush_NewStore_ReadsSameProgress()
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
        public async Task WriteThenFlush_File_StoresCurrentVersionUnderGameplayKey()
        {
            // Arrange
            var store = await CreateStoreAsync();
            store.Write(GameplaySave.Section, new GameplaySaveDto(BestScore: 70, RoundsPlayed: 4));

            // Act
            await store.FlushAsync(CancellationToken.None);

            // Assert
            var section = JObject.Parse(_files[SlotPath])["sections"]!["gameplay"]!;
            section["version"]!.Value<int>().Should().Be(GameplaySave.CurrentVersion);
            JToken.DeepEquals(section["data"], JObject.Parse("{\"bestScore\":70,\"roundsPlayed\":4}")).Should().BeTrue();
        }

        private async UniTask<SaveStore> CreateStoreAsync()
        {
            var store = new SaveStore(_storage, new JsonSerializer(), new FakeClock(Now));
            await store.SelectSlotAsync(0, CancellationToken.None);
            return store;
        }

        private UniTask<OneOf<string, NotFound, Error>> ReadFile(string path)
        {
            if (_files.TryGetValue(path, out var content))
            {
                return UniTask.FromResult<OneOf<string, NotFound, Error>>(content);
            }

            return UniTask.FromResult<OneOf<string, NotFound, Error>>(new NotFound());
        }

        private UniTask<OneOf<Success, Error>> WriteFile(string path, string content)
        {
            _files[path] = content;
            return UniTask.FromResult<OneOf<Success, Error>>(new Success());
        }
    }
}
