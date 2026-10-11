using System;
using System.Collections.Generic;

namespace Core.Telemetry
{
    internal sealed class TelemetryQueue
    {
        public int Count { get; private set; }

        public int DroppedCount { get; private set; }

        public bool IsDirty { get; private set; }

        private readonly int _capacity;
        private readonly List<Group> _groups = new();

        public TelemetryQueue(int capacity = TelemetryLimits.MaxQueuedEvents)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            _capacity = capacity;
        }

        public void Enqueue(TelemetryEnvelope envelope, TelemetryEvent telemetryEvent)
        {
            var last = _groups.Count > 0 ? _groups[_groups.Count - 1] : null;

            if (last == null || last.Envelope != envelope)
            {
                last = new Group(envelope);
                _groups.Add(last);
            }

            last.Events.Add(telemetryEvent);
            Count++;
            IsDirty = true;
            Trim();
        }

        public void Restore(TelemetryQueueDto dto)
        {
            var restored = new List<Group>();

            foreach (var groupDto in dto.Groups ?? Array.Empty<TelemetryQueueGroupDto>())
            {
                if (groupDto.Envelope == null || groupDto.Events == null || groupDto.Events.Length == 0)
                {
                    continue;
                }

                var group = new Group(groupDto.Envelope);
                group.Events.AddRange(groupDto.Events);
                restored.Add(group);
                Count += group.Events.Count;
            }

            _groups.InsertRange(0, restored);
            DroppedCount += dto.DroppedCount ?? 0;
            IsDirty = true;
            Trim();
        }

        public TelemetryBatch? PeekBatch(int maxEvents = TelemetryLimits.MaxBatchEvents)
        {
            if (_groups.Count == 0)
            {
                return null;
            }

            var first = _groups[0];
            var count = Math.Min(maxEvents, first.Events.Count);
            return new TelemetryBatch(first.Envelope, first.Events.GetRange(0, count));
        }

        public void Remove(TelemetryBatch batch)
        {
            var group = _groups.Find(candidate => candidate.Envelope == batch.Envelope);

            if (group == null)
            {
                return;
            }

            var removed = group.Events.RemoveAll(telemetryEvent => Contains(batch.Events, telemetryEvent));
            Count -= removed;
            IsDirty |= removed > 0;

            if (group.Events.Count == 0)
            {
                _groups.Remove(group);
            }
        }

        public int TakeDroppedCount()
        {
            var dropped = DroppedCount;
            DroppedCount = 0;
            IsDirty |= dropped > 0;
            return dropped;
        }

        public void Clear()
        {
            _groups.Clear();
            Count = 0;
            DroppedCount = 0;
            IsDirty = false;
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public TelemetryQueueDto TakeSnapshot()
        {
            var groups = new TelemetryQueueGroupDto[_groups.Count];

            for (var i = 0; i < _groups.Count; i++)
            {
                groups[i] = new TelemetryQueueGroupDto(_groups[i].Envelope, _groups[i].Events.ToArray());
            }

            IsDirty = false;
            return new TelemetryQueueDto(TelemetryQueueDto.CurrentVersion, DroppedCount, groups);
        }

        private void Trim()
        {
            while (Count > _capacity)
            {
                var first = _groups[0];
                var excess = Math.Min(Count - _capacity, first.Events.Count);
                first.Events.RemoveRange(0, excess);
                Count -= excess;
                DroppedCount += excess;

                if (first.Events.Count == 0)
                {
                    _groups.RemoveAt(0);
                }
            }
        }

        private static bool Contains(IReadOnlyList<TelemetryEvent> events, TelemetryEvent telemetryEvent)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (ReferenceEquals(events[i], telemetryEvent))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class Group
        {
            public TelemetryEnvelope Envelope { get; }

            public List<TelemetryEvent> Events { get; } = new();

            public Group(TelemetryEnvelope envelope)
            {
                Envelope = envelope;
            }
        }
    }
}
