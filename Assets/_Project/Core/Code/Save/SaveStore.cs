using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Core.Logging;
using Core.Results;
using Core.Storage;
using Core.Time;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using Success = OneOf.Types.Success;

namespace Core.Save
{
    public sealed class SaveStore : ISaveStore
    {
        public const int CurrentFormatVersion = 1;

        private const int MaxBackupPathAttempts = 10;

        public int ActiveSlot
        {
            get
            {
                EnsureSlotSelected();
                return _activeSlot;
            }
        }

        private int _activeSlot;
        private bool _isSlotSelected;
        private string? _overwriteRefusal;
        private long _changeCount;
        private long _flushedChangeCount;
        private Dictionary<string, StoredSection> _sections = new();
        private Dictionary<string, JObject> _newerSectionSessionData = new();
        private HashSet<string> _reportedNewerSections = new();

        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private readonly IRealClock _clock;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public SaveStore(IFileStorage storage, IJsonSerializer serializer, IRealClock clock)
        {
            _storage = storage;
            _serializer = serializer;
            _clock = clock;
        }

        public async UniTask<OneOf<Success, Error>> SelectSlotAsync(int slot, CancellationToken ct)
        {
            ValidateSlot(slot);
            await _gate.WaitAsync(ct);

            try
            {
                var read = await _storage.ReadAsync(GetSlotPath(slot), ct);

                if (read.TryPickT1(out _, out var readRemainder))
                {
                    Activate(slot, new Dictionary<string, StoredSection>(), overwriteRefusal: null);
                    return new Success();
                }

                if (readRemainder.TryPickT1(out var readError, out var content))
                {
                    Activate(slot, new Dictionary<string, StoredSection>(), $"it could not be read: {readError.Message}");
                    return new Error($"Save slot {slot} could not be read, it will not be overwritten: {readError.Message}");
                }

                var parsed = ParseFile(content);

                if (parsed.TryPickT0(out var sections, out var parsedRemainder))
                {
                    Activate(slot, sections, overwriteRefusal: null);
                    return new Success();
                }

                if (parsedRemainder.TryPickT0(out var newerFormat, out var corrupted))
                {
                    var reason = $"it was written by a newer game version (format version {newerFormat.Version}, supported {CurrentFormatVersion})";
                    Activate(slot, new Dictionary<string, StoredSection>(), reason);
                    return new Error($"Save slot {slot} is kept unchanged because {reason}. The slot starts empty and nothing will be saved to it.");
                }

                return await RecoverCorruptedSlotAsync(slot, content, corrupted, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public OneOf<T, NotFound, Corrupted> Read<T>(SaveSection<T> section) where T : class
        {
            EnsureSlotSelected();
            ValidateSection(section);

            if (!_sections.TryGetValue(section.Key, out var stored))
            {
                return new NotFound();
            }

            if (stored.Version > section.CurrentVersion)
            {
                ReportNewerSection(section, stored);
                return _newerSectionSessionData.TryGetValue(section.Key, out var sessionData)
                    ? Deserialize(section, sessionData)
                    : new NotFound();
            }

            var upgraded = Upgrade(section, stored);

            if (!upgraded.TryPickT0(out var data, out var corrupted))
            {
                return corrupted;
            }

            return Deserialize(section, data);
        }

        public void Write<T>(SaveSection<T> section, T data) where T : class
        {
            EnsureSlotSelected();
            ValidateSection(section);

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (ParseWithoutDates(_serializer.Serialize(data)) is not JObject serialized)
            {
                throw new ArgumentException($"Data of save section '{section.Key}' must serialize to a JSON object, but {typeof(T).Name} does not.", nameof(data));
            }

            if (_sections.TryGetValue(section.Key, out var stored) && stored.Version > section.CurrentVersion)
            {
                ReportNewerSection(section, stored);
                _newerSectionSessionData[section.Key] = serialized;
                return;
            }

            _sections[section.Key] = new StoredSection(section.CurrentVersion, serialized);
            _changeCount++;
        }

        public async UniTask<OneOf<Success, Error>> FlushAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);

            try
            {
                var changeCount = _changeCount;

                if (changeCount == _flushedChangeCount)
                {
                    return new Success();
                }

                if (_overwriteRefusal is not null)
                {
                    return new Error($"Refusing to overwrite save slot {_activeSlot} because {_overwriteRefusal}.");
                }

                var json = _serializer.Serialize(CreateFileDto());
                var written = await _storage.WriteAsync(GetSlotPath(_activeSlot), json, ct);

                if (written.IsT0)
                {
                    _flushedChangeCount = changeCount;
                }

                return written;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask<OneOf<Success, Error>> DeleteSlotAsync(int slot, CancellationToken ct)
        {
            ValidateSlot(slot);
            await _gate.WaitAsync(ct);

            try
            {
                var deleted = _storage.Delete(GetSlotPath(slot));

                if (deleted.IsT0 && _isSlotSelected && slot == _activeSlot)
                {
                    Activate(slot, new Dictionary<string, StoredSection>(), overwriteRefusal: null);
                }

                return deleted;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async UniTask<OneOf<Success, Error>> RecoverCorruptedSlotAsync(int slot, string content, Corrupted corrupted, CancellationToken ct)
        {
            if (!FindFreeBackupPath(slot).TryPickT0(out var backupPath, out var pathError))
            {
                Activate(slot, new Dictionary<string, StoredSection>(), $"it is corrupted and could not be backed up: {pathError.Message}");
                return new Error($"Save slot {slot} is corrupted ({corrupted.Reason}) and could not be backed up, it will not be overwritten: {pathError.Message}");
            }

            var backup = await _storage.WriteAsync(backupPath, content, ct);

            if (backup.TryPickT1(out var backupError, out _))
            {
                Activate(slot, new Dictionary<string, StoredSection>(), $"it is corrupted and could not be backed up: {backupError.Message}");
                return new Error($"Save slot {slot} is corrupted ({corrupted.Reason}) and could not be backed up, it will not be overwritten: {backupError.Message}");
            }

            Activate(slot, new Dictionary<string, StoredSection>(), overwriteRefusal: null);
            return new Error($"Save slot {slot} is corrupted ({corrupted.Reason}), it was backed up to '{backupPath}' and the slot starts empty.");
        }

        private OneOf<string, Error> FindFreeBackupPath(int slot)
        {
            var stamp = _clock.UtcNow.ToString("yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture);

            for (var attempt = 0; attempt < MaxBackupPathAttempts; attempt++)
            {
                var path = GetCorruptedBackupPath(slot, stamp, attempt);

                if (!_storage.Exists(path))
                {
                    return path;
                }
            }

            return new Error($"{MaxBackupPathAttempts} backups of save slot {slot} already exist for {stamp}.");
        }

        private static JToken ParseWithoutDates(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            return JToken.ReadFrom(reader);
        }

        private OneOf<Dictionary<string, StoredSection>, NewerFormat, Corrupted> ParseFile(string content)
        {
            if (!_serializer.Deserialize<JObject>(content).TryPickT0(out var root, out var rootCorrupted))
            {
                return rootCorrupted;
            }

            if (root["formatVersion"] is JValue { Value: long formatVersion } && formatVersion > CurrentFormatVersion)
            {
                return new NewerFormat(formatVersion);
            }

            if (!_serializer.Deserialize<SaveFileDto>(content).TryPickT0(out var file, out var corrupted))
            {
                return corrupted;
            }

            if (file.FormatVersion != CurrentFormatVersion)
            {
                return new Corrupted($"Unsupported format version {file.FormatVersion}, expected {CurrentFormatVersion}.");
            }

            if (file.Sections is null)
            {
                return new Corrupted("The file has no sections.");
            }

            var sections = new Dictionary<string, StoredSection>();

            foreach (var (key, section) in file.Sections)
            {
                if (section is null || section.Data is null || section.Version < 1)
                {
                    return new Corrupted($"Section '{key}' has no data or an invalid version.");
                }

                sections[key] = new StoredSection(section.Version, section.Data);
            }

            return sections;
        }

        private OneOf<JObject, Corrupted> Upgrade<T>(SaveSection<T> section, StoredSection stored) where T : class
        {
            if (stored.Version == section.CurrentVersion)
            {
                return stored.Data;
            }

            var migrated = section.Migrate((JObject)stored.Data.DeepClone(), stored.Version);

            if (migrated.TryPickT0(out var data, out _))
            {
                _sections[section.Key] = new StoredSection(section.CurrentVersion, data);
            }

            return migrated;
        }

        private OneOf<T, NotFound, Corrupted> Deserialize<T>(SaveSection<T> section, JObject data) where T : class
        {
            return _serializer.Deserialize<T>(data.ToString(Formatting.None)).Match<OneOf<T, NotFound, Corrupted>>(
                value => value,
                corrupted => new Corrupted($"Section '{section.Key}' does not match {typeof(T).Name}: {corrupted.Reason}"));
        }

        private void ReportNewerSection<T>(SaveSection<T> section, StoredSection stored) where T : class
        {
            if (_reportedNewerSections.Add(section.Key))
            {
                Log.Warn(LogTags.Save, $"Save section '{section.Key}' was written by a newer game version (version {stored.Version}, supported {section.CurrentVersion}). It is kept unchanged on disk; this session uses defaults and does not save changes to it.");
            }
        }

        private SaveFileDto CreateFileDto()
        {
            var sections = new Dictionary<string, SaveSectionDto?>();

            foreach (var (key, section) in _sections)
            {
                sections[key] = new SaveSectionDto(section.Version, section.Data);
            }

            return new SaveFileDto(CurrentFormatVersion, _clock.UtcNow, sections);
        }

        private void Activate(int slot, Dictionary<string, StoredSection> sections, string? overwriteRefusal)
        {
            _activeSlot = slot;
            _isSlotSelected = true;
            _overwriteRefusal = overwriteRefusal;
            _sections = sections;
            _newerSectionSessionData = new Dictionary<string, JObject>();
            _reportedNewerSections = new HashSet<string>();
            _changeCount = 0;
            _flushedChangeCount = 0;
        }

        private void EnsureSlotSelected()
        {
            if (!_isSlotSelected)
            {
                throw new InvalidOperationException("No save slot is selected yet. Core selects one at startup, before any domain runs.");
            }
        }

        private static string GetSlotPath(int slot)
        {
            return $"Saves/slot_{slot}.json";
        }

        private static string GetCorruptedBackupPath(int slot, string stamp, int attempt)
        {
            var suffix = attempt == 0 ? string.Empty : $"_{attempt}";
            return $"Saves/slot_{slot}.corrupted.{stamp}{suffix}.json";
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Save slot index cannot be negative.");
            }
        }

        private static void ValidateSection<T>(SaveSection<T> section) where T : class
        {
            if (string.IsNullOrWhiteSpace(section.Key))
            {
                throw new ArgumentException("Save section key cannot be empty.", nameof(section));
            }

            if (section.CurrentVersion < 1)
            {
                throw new ArgumentException($"Save section '{section.Key}' must have a version of at least 1, but has {section.CurrentVersion}.", nameof(section));
            }
        }

        private readonly record struct StoredSection(int Version, JObject Data);

        private readonly record struct NewerFormat(long Version);
    }
}
