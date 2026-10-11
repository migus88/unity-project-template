using System.Linq;
using AwesomeAssertions;
using Core.Telemetry;
using NUnit.Framework;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryQueueTests
    {
        [Test]
        public void PeekBatch_SameEnvelope_ReturnsEventsInOneBatch()
        {
            // Arrange
            var queue = new TelemetryQueue();
            queue.Enqueue(TelemetryTestData.Envelope(), TelemetryTestData.Event("first"));
            queue.Enqueue(TelemetryTestData.Envelope(), TelemetryTestData.Event("second"));

            // Act
            var batch = queue.PeekBatch();

            // Assert
            batch!.Events.Select(e => e.Name).Should().Equal("first", "second");
        }

        [Test]
        public void PeekBatch_NewEnvelope_StopsAtTheGroupBoundary()
        {
            // Arrange
            var queue = new TelemetryQueue();
            var older = TelemetryTestData.Envelope("00000000-0000-0000-0000-000000000001");
            queue.Enqueue(older, TelemetryTestData.Event("first"));
            queue.Enqueue(TelemetryTestData.Envelope("00000000-0000-0000-0000-000000000002"), TelemetryTestData.Event("second"));

            // Act
            var batch = queue.PeekBatch();

            // Assert
            batch!.Envelope.Should().Be(older);
            batch.Events.Select(e => e.Name).Should().Equal("first");
        }

        [Test]
        public void PeekBatch_MoreThanMaxEvents_CapsTheBatch()
        {
            // Arrange
            var queue = new TelemetryQueue();

            for (var i = 0; i <= TelemetryLimits.MaxBatchEvents; i++)
            {
                queue.Enqueue(TelemetryTestData.Envelope(), TelemetryTestData.Event("tick", i));
            }

            // Act
            var batch = queue.PeekBatch();

            // Assert
            batch!.Events.Should().HaveCount(TelemetryLimits.MaxBatchEvents);
        }

        [Test]
        public void Enqueue_OverCapacity_DropsOldestAcrossGroupsAndCountsThem()
        {
            // Arrange
            var queue = new TelemetryQueue(3);
            var older = TelemetryTestData.Envelope("00000000-0000-0000-0000-000000000001");
            var newer = TelemetryTestData.Envelope("00000000-0000-0000-0000-000000000002");
            queue.Enqueue(older, TelemetryTestData.Event("old_a"));
            queue.Enqueue(older, TelemetryTestData.Event("old_b"));
            queue.Enqueue(newer, TelemetryTestData.Event("new_a"));
            queue.Enqueue(newer, TelemetryTestData.Event("new_b"));

            // Act
            queue.Enqueue(newer, TelemetryTestData.Event("new_c"));

            // Assert
            queue.Count.Should().Be(3);
            queue.DroppedCount.Should().Be(2);
            queue.PeekBatch()!.Events.Select(e => e.Name).Should().Equal("new_a", "new_b", "new_c");
        }

        [Test]
        public void Remove_SentBatch_KeepsTheRemainderInOrder()
        {
            // Arrange
            var queue = new TelemetryQueue();

            for (var i = 0; i < 5; i++)
            {
                queue.Enqueue(TelemetryTestData.Envelope(), TelemetryTestData.Event($"event_{i}", i));
            }

            var sent = queue.PeekBatch(2)!;

            // Act
            queue.Remove(sent);

            // Assert
            queue.PeekBatch()!.Events.Select(e => e.Seq).Should().Equal(2L, 3L, 4L);
        }
    }
}
