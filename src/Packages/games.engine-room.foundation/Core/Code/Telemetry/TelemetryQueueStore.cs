using System.Threading;
using Core.Logging;
using Core.Storage;
using Cysharp.Threading.Tasks;

namespace Core.Telemetry
{
    internal sealed class TelemetryQueueStore
    {
        public const string Path = "telemetry/queue.json";

        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;

        public TelemetryQueueStore(IFileStorage storage, IJsonSerializer serializer)
        {
            _storage = storage;
            _serializer = serializer;
        }

        public async UniTask<TelemetryQueueDto?> LoadAsync(CancellationToken ct)
        {
            var read = await _storage.ReadAsync(Path, ct);

            if (read.TryPickT1(out _, out var rest))
            {
                return null;
            }

            if (rest.TryPickT1(out var error, out var json))
            {
                return Discard($"could not be read: {error.Message}");
            }

            var parsed = _serializer.Deserialize<TelemetryQueueDto>(json);

            if (parsed.TryPickT1(out var corrupted, out var dto))
            {
                return Discard($"is corrupted: {corrupted.Reason}");
            }

            if (dto.Version != TelemetryQueueDto.CurrentVersion)
            {
                return Discard($"has version {dto.Version}, expected {TelemetryQueueDto.CurrentVersion}");
            }

            return dto;
        }

        public async UniTask<bool> SaveAsync(TelemetryQueueDto dto, CancellationToken ct)
        {
            var written = await _storage.WriteAsync(Path, _serializer.Serialize(dto), ct);

            if (written.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Telemetry, $"Telemetry queue could not be saved: {error.Message}");
                return false;
            }

            return true;
        }

        public void Delete()
        {
            if (!_storage.Exists(Path))
            {
                return;
            }

            var deleted = _storage.Delete(Path);

            if (deleted.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Telemetry, $"Telemetry queue could not be deleted: {error.Message}");
            }
        }

        private TelemetryQueueDto? Discard(string reason)
        {
            Log.Warn(LogTags.Telemetry, $"Telemetry queue {reason}; discarding it.");
            Delete();
            return null;
        }
    }
}
