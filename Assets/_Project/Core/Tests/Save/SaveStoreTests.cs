using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Save;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using OneOf;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;
using Success = OneOf.Types.Success;

namespace Core.Tests.Save
{
    public sealed class SaveStoreTests
    {
        private const string SlotZeroPath = "Saves/slot_0.json";
        private const string SlotOnePath = "Saves/slot_1.json";
        private const string SlotZeroBackupPath = "Saves/slot_0.corrupted.20260928T120000000.json";
        private const string NewerProgressJson = "{\"level\":3,\"playerName\":\"Ada\",\"title\":\"Knight\"}";

        private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        private static readonly SaveSection<ProgressDto> ProgressSection = new("progress", 1, FailMigration);

        private int _migrationCount;
        private InMemoryFileStorage _disk = null!;
        private IFileStorage _storage = null!;
        private FakeClock _clock = null!;
        private SaveStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _migrationCount = 0;
            _disk = new InMemoryFileStorage();
            _storage = Substitute.For<IFileStorage>();
            _storage.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => _disk.ReadAsync(call.ArgAt<string>(0), call.ArgAt<CancellationToken>(1)));
            _storage.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => _disk.WriteAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
            _storage.Delete(Arg.Any<string>()).Returns(call => _disk.Delete(call.ArgAt<string>(0)));
            _storage.Exists(Arg.Any<string>()).Returns(call => _disk.Exists(call.ArgAt<string>(0)));
            _clock = new FakeClock(Now);
            _store = CreateStore();
        }

        [Test]
        public async Task SelectSlotAsync_MissingFile_SelectsEmptySlot()
        {
            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _store.ActiveSlot.Should().Be(0);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
            _ = _storage.Received(1).ReadAsync(SlotZeroPath, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SelectSlotAsync_ExistingFile_LoadsSections()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));

            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _store.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Should().Be(new ProgressDto(3, "Ada"));
        }

        [Test]
        public async Task SelectSlotAsync_OtherSlot_ReplacesSectionsInMemory()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Act
            var result = await _store.SelectSlotAsync(1, CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _store.ActiveSlot.Should().Be(1);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
            _ = _storage.Received(1).ReadAsync(SlotOnePath, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SelectSlotAsync_NegativeSlot_Throws()
        {
            // Act
            Func<Task> act = () => _store.SelectSlotAsync(-1, CancellationToken.None).AsTask();

            // Assert
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task SelectSlotAsync_Cancelled_ThrowsAndKeepsActiveSlot()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = () => _store.SelectSlotAsync(1, cts.Token).AsTask();

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _store.ActiveSlot.Should().Be(0);
        }

        [Test]
        public async Task SelectSlotAsync_ReadError_ReturnsErrorAndSelectsEmptySlot()
        {
            // Arrange
            _storage.Configure().ReadAsync(SlotZeroPath, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<string, NotFound, Error>>(new Error("disk on fire")));

            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Contain("disk on fire");
            _store.ActiveSlot.Should().Be(0);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
        }

        [Test]
        public async Task FlushAsync_AfterReadError_RefusesToOverwriteSlot()
        {
            // Arrange
            _storage.Configure().ReadAsync(SlotZeroPath, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<string, NotFound, Error>>(new Error("disk on fire")));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>();
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [TestCase("{\"formatVersion\":")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{\"formatVersion\":0,\"savedAtUtc\":\"2026-09-28T12:00:00Z\",\"sections\":{}}")]
        [TestCase("{\"savedAtUtc\":\"2026-09-28T12:00:00Z\",\"sections\":{}}")]
        [TestCase("{\"formatVersion\":1,\"savedAtUtc\":\"2026-09-28T12:00:00Z\"}")]
        [TestCase("{\"formatVersion\":1,\"sections\":{\"progress\":null}}")]
        [TestCase("{\"formatVersion\":1,\"sections\":{\"progress\":{\"version\":1}}}")]
        [TestCase("{\"formatVersion\":1,\"sections\":{\"progress\":{\"version\":0,\"data\":{}}}}")]
        [TestCase("{\"formatVersion\":1,\"sections\":{\"progress\":{\"version\":1,\"data\":[1,2]}}}")]
        [TestCase("{\"formatVersion\":1,\"sections\":{\"progress\":{\"version\":\"one\",\"data\":{}}}}")]
        public async Task SelectSlotAsync_CorruptedFile_BacksItUpAndSelectsEmptySlot(string content)
        {
            // Arrange
            _disk.Files[SlotZeroPath] = content;

            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Contain("corrupted");
            _disk.Files[SlotZeroBackupPath].Should().Be(content);
            _store.ActiveSlot.Should().Be(0);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
        }

        [Test]
        public async Task FlushAsync_AfterCorruptedFileWasBackedUp_OverwritesSlot()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = "{broken";
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(2, "Ada"));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.Files[SlotZeroPath].Should().Contain("\"progress\"");
            _disk.Files[SlotZeroBackupPath].Should().Be("{broken");
        }

        [Test]
        public async Task SelectSlotAsync_CorruptedFileAndBackupFails_RefusesToOverwriteSlot()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = "{broken";
            _storage.Configure().WriteAsync(SlotZeroBackupPath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<OneOf<Success, Error>>(new Error("read-only")));
            var selected = await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(2, "Ada"));

            // Act
            var flushed = await _store.FlushAsync(CancellationToken.None);

            // Assert
            selected.Should().BeCase<Error>().Which.Message.Should().Contain("read-only");
            flushed.Should().BeCase<Error>();
            _disk.Files[SlotZeroPath].Should().Be("{broken");
        }

        [Test]
        public async Task SelectSlotAsync_CorruptedAgainAtSameTime_KeepsEarlierBackup()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = "{first";
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _disk.Files[SlotZeroPath] = "{second";

            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Contain("slot_0.corrupted.20260928T120000000_1.json");
            _disk.Files[SlotZeroBackupPath].Should().Be("{first");
            _disk.Files["Saves/slot_0.corrupted.20260928T120000000_1.json"].Should().Be("{second");
        }

        [Test]
        public async Task SelectSlotAsync_CorruptedAgainLater_KeepsEarlierBackup()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = "{first";
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _disk.Files[SlotZeroPath] = "{second";
            _clock.Advance(TimeSpan.FromMinutes(1.5));

            // Act
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            _disk.Files[SlotZeroBackupPath].Should().Be("{first");
            _disk.Files["Saves/slot_0.corrupted.20260928T120130000.json"].Should().Be("{second");
        }

        [Test]
        public async Task SelectSlotAsync_CorruptedAndEveryBackupPathTaken_RefusesToOverwriteSlot()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = "{broken";
            _disk.Files[SlotZeroBackupPath] = "old";

            for (var attempt = 1; attempt < 10; attempt++)
            {
                _disk.Files[$"Saves/slot_0.corrupted.20260928T120000000_{attempt}.json"] = "old";
            }

            var selected = await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(2, "Ada"));

            // Act
            var flushed = await _store.FlushAsync(CancellationToken.None);

            // Assert
            selected.Should().BeCase<Error>().Which.Message.Should().Contain("could not be backed up");
            flushed.Should().BeCase<Error>();
            _disk.Files[SlotZeroPath].Should().Be("{broken");
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [Test]
        public void ActiveSlot_BeforeSelect_Throws()
        {
            // Act
            Action act = () => _ = _store.ActiveSlot;

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Read_BeforeSelect_Throws()
        {
            // Act
            Action act = () => _store.Read(ProgressSection);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Write_BeforeSelect_Throws()
        {
            // Act
            Action act = () => _store.Write(ProgressSection, new ProgressDto(1, "Ada"));

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task Write_ThenRead_ReturnsEqualData()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var progress = new ProgressDto(4, "Ada");

            // Act
            _store.Write(ProgressSection, progress);

            // Assert
            _store.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Should().Be(progress);
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [Test]
        public async Task Write_Twice_ReadReturnsLatestData()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));

            // Act
            _store.Write(ProgressSection, new ProgressDto(2, "Grace"));

            // Assert
            _store.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Should().Be(new ProgressDto(2, "Grace"));
        }

        [Test]
        public async Task Write_DataThatIsNotAJsonObject_Throws()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var section = new SaveSection<string>("name", 1, FailMigration);

            // Act
            Action act = () => _store.Write(section, "Ada");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [TestCase("", 1)]
        [TestCase(" ", 1)]
        [TestCase("progress", 0)]
        public async Task Write_InvalidSection_Throws(string key, int version)
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var section = new SaveSection<ProgressDto>(key, version, FailMigration);

            // Act
            Action act = () => _store.Write(section, new ProgressDto(1, "Ada"));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public async Task Read_DataThatDoesNotMatchDto_ReturnsCorrupted()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":\"high\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Act
            var result = _store.Read(ProgressSection);

            // Assert
            result.Should().BeCase<Corrupted>().Which.Reason.Should().Contain("progress");
        }

        [Test]
        public async Task Read_NewerSectionVersion_ReturnsNotFoundAndWarnsOnce()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 2, NewerProgressJson));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, "[Save] Save section 'progress' was written by a newer game version (version 2, supported 1). It is kept unchanged on disk; this session uses defaults and does not save changes to it.");

            // Act
            var first = _store.Read(ProgressSection);
            var second = _store.Read(ProgressSection);

            // Assert
            first.Should().BeCase<NotFound>();
            second.Should().BeCase<NotFound>();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task Write_NewerSectionVersion_ReadReturnsWrittenDataAndWarnsOnce()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 2, NewerProgressJson));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("'progress' was written by a newer game version"));

            // Act
            _store.Write(ProgressSection, new ProgressDto(1, "Bob"));
            var result = _store.Read(ProgressSection);

            // Assert
            result.Should().BeCase<ProgressDto>().Which.Should().Be(new ProgressDto(1, "Bob"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task FlushAsync_AfterWriteToNewerSection_KeepsTheNewerSectionUnchanged()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 2, NewerProgressJson));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("newer game version"));
            _store.Read(ProgressSection);
            _store.Write(ProgressSection, new ProgressDto(1, "Bob"));
            _store.Write(ScoreSectionV3(), new ScoreDto(5, 1));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            var sections = JObject.Parse(_disk.Files[SlotZeroPath])["sections"]!;
            JToken.DeepEquals(sections["progress"], JObject.Parse($"{{\"version\":2,\"data\":{NewerProgressJson}}}")).Should().BeTrue();
            sections["score"]!["data"]!["bestScore"]!.Value<int>().Should().Be(5);
        }

        [Test]
        public async Task FlushAsync_OnlyNewerSectionWritten_DoesNotWrite()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 2, NewerProgressJson));
            var original = _disk.Files[SlotZeroPath];
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("newer game version"));
            _store.Write(ProgressSection, new ProgressDto(1, "Bob"));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.WrittenPaths.Should().BeEmpty();
            _disk.Files[SlotZeroPath].Should().Be(original);
        }

        [Test]
        public async Task SelectSlotAsync_AfterNewerSectionSession_ForgetsSessionData()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 2, NewerProgressJson));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("newer game version"));
            _store.Write(ProgressSection, new ProgressDto(1, "Bob"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            LogAssert.Expect(LogType.Warning, new Regex("newer game version"));

            // Act
            var result = _store.Read(ProgressSection);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [TestCase("{\"formatVersion\":2,\"savedAtUtc\":\"2026-09-28T12:00:00Z\",\"sections\":{}}")]
        [TestCase("{\"formatVersion\":7,\"sections\":[{\"key\":\"progress\"}],\"extra\":true}")]
        public async Task SelectSlotAsync_NewerFormatVersion_ReturnsErrorWithoutBackupAndSelectsEmptySlot(string content)
        {
            // Arrange
            _disk.Files[SlotZeroPath] = content;

            // Act
            var result = await _store.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Contain("newer game version");
            _store.ActiveSlot.Should().Be(0);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
            _disk.Files.Keys.Should().Equal(SlotZeroPath);
        }

        [Test]
        public async Task FlushAsync_AfterNewerFormatVersion_RefusesToOverwriteSlot()
        {
            // Arrange
            const string content = "{\"formatVersion\":2,\"savedAtUtc\":\"2026-09-28T12:00:00Z\",\"sections\":{\"progress\":{\"version\":1,\"data\":{}}}}";
            _disk.Files[SlotZeroPath] = content;
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>().Which.Message.Should().Contain("newer game version");
            _disk.WrittenPaths.Should().BeEmpty();
            _disk.Files[SlotZeroPath].Should().Be(content);
        }

        [Test]
        public async Task Read_OlderSectionVersion_MigratesToCurrentVersion()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("score", 1, "{\"score\":42}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Act
            var result = _store.Read(ScoreSectionV3());

            // Assert
            result.Should().BeCase<ScoreDto>().Which.Should().Be(new ScoreDto(42, 0));
        }

        [Test]
        public async Task Read_MigratedSection_MigratesOnlyOnce()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("score", 1, "{\"score\":42}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var section = ScoreSectionV3();
            _store.Read(section);

            // Act
            var result = _store.Read(section);

            // Assert
            result.Should().BeCase<ScoreDto>();
            _migrationCount.Should().Be(1);
        }

        [Test]
        public async Task FlushAsync_AfterMigrationAndWrite_PersistsCurrentVersion()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("score", 2, "{\"bestScore\":7}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var section = ScoreSectionV3();
            _store.Read(section);
            _store.Write(section, new ScoreDto(8, 1));

            // Act
            await _store.FlushAsync(CancellationToken.None);

            // Assert
            var score = JObject.Parse(_disk.Files[SlotZeroPath])["sections"]!["score"]!;
            score["version"]!.Value<int>().Should().Be(3);
            score["data"]!["bestScore"]!.Value<int>().Should().Be(8);
        }

        [Test]
        public async Task Read_MigrationFails_ReturnsCorruptedAndKeepsStoredData()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("score", 1, "{\"score\":42}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var failing = new SaveSection<ScoreDto>("score", 3, (data, _) =>
            {
                data.RemoveAll();
                return new Corrupted("unsupported");
            });

            // Act
            var result = _store.Read(failing);

            // Assert
            result.Should().BeCase<Corrupted>().Which.Reason.Should().Be("unsupported");
            _store.Read(ScoreSectionV3()).Should().BeCase<ScoreDto>().Which.BestScore.Should().Be(42);
        }

        [Test]
        public async Task FlushAsync_AfterWrite_WritesVersionedEnvelope()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(5, "Ada"));

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            var file = JObject.Parse(_disk.Files[SlotZeroPath]);
            file["formatVersion"]!.Value<int>().Should().Be(1);
            file["savedAtUtc"]!.Value<DateTime>().Should().Be(Now);
            file["sections"]!["progress"]!["version"]!.Value<int>().Should().Be(1);
            file["sections"]!["progress"]!["data"]!["level"]!.Value<int>().Should().Be(5);
            file["sections"]!["progress"]!["data"]!["playerName"]!.Value<string>().Should().Be("Ada");
        }

        [Test]
        public async Task FlushAsync_ThenSelectSlotInNewStore_RoundTripsSections()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(5, "Ada"));
            _store.Write(ScoreSectionV3(), new ScoreDto(99, 3));
            await _store.FlushAsync(CancellationToken.None);
            var reloaded = CreateStore();

            // Act
            await reloaded.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            reloaded.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Should().Be(new ProgressDto(5, "Ada"));
            reloaded.Read(ScoreSectionV3()).Should().BeCase<ScoreDto>().Which.Should().Be(new ScoreDto(99, 3));
            _migrationCount.Should().Be(0);
        }

        [Test]
        public async Task FlushAsync_ThenSelectSlotInNewStore_KeepsDateLikeStringsUnchanged()
        {
            // Arrange
            var progress = new ProgressDto(5, "2025-05-01T10:00:00+02:00");
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, progress);
            await _store.FlushAsync(CancellationToken.None);
            var reloaded = CreateStore();

            // Act
            await reloaded.SelectSlotAsync(0, CancellationToken.None);

            // Assert
            reloaded.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Should().Be(progress);
        }

        [Test]
        public async Task FlushAsync_UnreadSection_KeepsIt()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("orphan", 4, "{\"value\":1}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));

            // Act
            await _store.FlushAsync(CancellationToken.None);

            // Assert
            var orphan = JObject.Parse(_disk.Files[SlotZeroPath])["sections"]!["orphan"]!;
            orphan["version"]!.Value<int>().Should().Be(4);
            orphan["data"]!["value"]!.Value<int>().Should().Be(1);
        }

        [Test]
        public async Task FlushAsync_WithoutChanges_DoesNotWrite()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Read(ProgressSection);

            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [Test]
        public async Task FlushAsync_BeforeSelect_DoesNotWrite()
        {
            // Act
            var result = await _store.FlushAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [Test]
        public async Task FlushAsync_Twice_WritesOnce()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));
            await _store.FlushAsync(CancellationToken.None);

            // Act
            await _store.FlushAsync(CancellationToken.None);

            // Assert
            _disk.WrittenPaths.Should().Equal(SlotZeroPath);
        }

        [Test]
        public async Task FlushAsync_StorageFails_ReturnsErrorAndRetriesOnNextFlush()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));
            _storage.Configure().WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
                UniTask.FromResult<OneOf<Success, Error>>(new Error("disk full")),
                UniTask.FromResult<OneOf<Success, Error>>(new Success()));

            // Act
            var first = await _store.FlushAsync(CancellationToken.None);
            var second = await _store.FlushAsync(CancellationToken.None);

            // Assert
            first.Should().BeCase<Error>().Which.Message.Should().Be("disk full");
            second.Should().BeCase<Success>();
            _ = _storage.Received(2).WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FlushAsync_WhileAnotherFlushIsWriting_WaitsForIt()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var firstWrite = new UniTaskCompletionSource<OneOf<Success, Error>>();
            _storage.Configure().WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
                firstWrite.Task,
                UniTask.FromResult<OneOf<Success, Error>>(new Success()));
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));
            var first = _store.FlushAsync(CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(2, "Ada"));

            // Act
            var second = _store.FlushAsync(CancellationToken.None);
            var secondStatusWhileFirstWrites = second.Status;
            var writesWhileFirstWrites = _storage.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IFileStorage.WriteAsync));
            firstWrite.TrySetResult(new Success());
            var firstResult = await first;
            var secondResult = await second;

            // Assert
            secondStatusWhileFirstWrites.Should().Be(UniTaskStatus.Pending);
            writesWhileFirstWrites.Should().Be(1);
            firstResult.Should().BeCase<Success>();
            secondResult.Should().BeCase<Success>();
            _ = _storage.Received(2).WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FlushAsync_WriteDuringFlush_StaysDirty()
        {
            // Arrange
            await _store.SelectSlotAsync(0, CancellationToken.None);
            var firstWrite = new UniTaskCompletionSource<OneOf<Success, Error>>();
            _storage.Configure().WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
                firstWrite.Task,
                UniTask.FromResult<OneOf<Success, Error>>(new Success()));
            _store.Write(ProgressSection, new ProgressDto(1, "Ada"));
            var first = _store.FlushAsync(CancellationToken.None);
            _store.Write(ProgressSection, new ProgressDto(2, "Ada"));
            firstWrite.TrySetResult(new Success());
            await first;

            // Act
            await _store.FlushAsync(CancellationToken.None);

            // Assert
            _ = _storage.Received(2).WriteAsync(SlotZeroPath, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteSlotAsync_ActiveSlot_DeletesFileAndClearsMemory()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Act
            var result = await _store.DeleteSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.Files.Should().NotContainKey(SlotZeroPath);
            _store.Read(ProgressSection).Should().BeCase<NotFound>();
            (await _store.FlushAsync(CancellationToken.None)).Should().BeCase<Success>();
            _disk.WrittenPaths.Should().BeEmpty();
        }

        [Test]
        public async Task DeleteSlotAsync_OtherSlot_KeepsActiveSlotData()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));
            _disk.Files[SlotOnePath] = FileJson(("progress", 1, "{\"level\":9,\"playerName\":\"Grace\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);

            // Act
            var result = await _store.DeleteSlotAsync(1, CancellationToken.None);

            // Assert
            result.Should().BeCase<Success>();
            _disk.Files.Should().NotContainKey(SlotOnePath);
            _store.Read(ProgressSection).Should().BeCase<ProgressDto>().Which.Level.Should().Be(3);
        }

        [Test]
        public async Task DeleteSlotAsync_StorageFails_ReturnsErrorAndKeepsData()
        {
            // Arrange
            _disk.Files[SlotZeroPath] = FileJson(("progress", 1, "{\"level\":3,\"playerName\":\"Ada\"}"));
            await _store.SelectSlotAsync(0, CancellationToken.None);
            _storage.Configure().Delete(SlotZeroPath).Returns(new Error("locked"));

            // Act
            var result = await _store.DeleteSlotAsync(0, CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>();
            _store.Read(ProgressSection).Should().BeCase<ProgressDto>();
        }

        [Test]
        public async Task DeleteSlotAsync_NegativeSlot_Throws()
        {
            // Act
            Func<Task> act = () => _store.DeleteSlotAsync(-1, CancellationToken.None).AsTask();

            // Assert
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        private SaveStore CreateStore()
        {
            return new SaveStore(_storage, new JsonSerializer(), _clock);
        }

        private SaveSection<ScoreDto> ScoreSectionV3()
        {
            return new SaveSection<ScoreDto>("score", 3, MigrateScore);
        }

        private OneOf<JObject, Corrupted> MigrateScore(JObject data, int fromVersion)
        {
            _migrationCount++;

            if (fromVersion < 2)
            {
                data["bestScore"] = data["score"];
                data.Remove("score");
            }

            if (fromVersion < 3)
            {
                data["attempts"] = 0;
            }

            return data;
        }

        private static OneOf<JObject, Corrupted> FailMigration(JObject data, int fromVersion)
        {
            return new Corrupted($"No migration from version {fromVersion}.");
        }

        private static string FileJson(params (string Key, int Version, string DataJson)[] sections)
        {
            var sectionsJson = new List<string>();

            foreach (var (key, version, dataJson) in sections)
            {
                sectionsJson.Add($"\"{key}\":{{\"version\":{version},\"data\":{dataJson}}}");
            }

            return $"{{\"formatVersion\":1,\"savedAtUtc\":\"2026-09-27T10:00:00Z\",\"sections\":{{{string.Join(",", sectionsJson)}}}}}";
        }

        public sealed record ProgressDto(int Level, string PlayerName);

        public sealed record ScoreDto(int BestScore, int Attempts);
    }
}
