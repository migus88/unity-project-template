using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Storage;
using Core.Telemetry;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryQueueStoreTests
    {
        private InMemoryFileStorage _storage = null!;
        private TelemetryQueueStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemoryFileStorage();
            _store = new TelemetryQueueStore(_storage, new JsonSerializer());
        }

        [Test]
        public async Task LoadAsync_AfterSave_RestoresEnvelopeEventsAndPropTypes()
        {
            // Arrange
            var props = new Dictionary<string, object> { ["mech_id"] = "atlas", ["turns"] = 7L, ["ratio"] = 0.5d, ["is_won"] = true };
            var queue = new TelemetryQueue();
            queue.Enqueue(TelemetryTestData.Envelope(), new TelemetryEvent("battle_ended", 4, 1234, props));
            await _store.SaveAsync(queue.TakeSnapshot(), CancellationToken.None);

            // Act
            var loaded = await _store.LoadAsync(CancellationToken.None);

            // Assert
            var group = loaded!.Groups!.Should().ContainSingle().Subject;
            group.Envelope.Should().Be(TelemetryTestData.Envelope());
            var loadedEvent = group.Events!.Should().ContainSingle().Subject;
            loadedEvent.Seq.Should().Be(4);
            loadedEvent.Props.Should().Equal(props);
        }

        [Test]
        public async Task LoadAsync_CorruptedFile_ReturnsNothingAndDeletesIt()
        {
            // Arrange
            _storage.Files[TelemetryQueueStore.Path] = "{ not json";
            LogAssert.Expect(LogType.Warning, new Regex("is corrupted"));

            // Act
            var loaded = await _store.LoadAsync(CancellationToken.None);

            // Assert
            loaded.Should().BeNull();
            _storage.Exists(TelemetryQueueStore.Path).Should().BeFalse();
        }

        [Test]
        public async Task LoadAsync_OtherVersion_ReturnsNothingAndDeletesIt()
        {
            // Arrange
            _storage.Files[TelemetryQueueStore.Path] = "{ \"version\": 99, \"groups\": [] }";
            LogAssert.Expect(LogType.Warning, new Regex("has version 99"));

            // Act
            var loaded = await _store.LoadAsync(CancellationToken.None);

            // Assert
            loaded.Should().BeNull();
            _storage.Exists(TelemetryQueueStore.Path).Should().BeFalse();
        }
    }
}
