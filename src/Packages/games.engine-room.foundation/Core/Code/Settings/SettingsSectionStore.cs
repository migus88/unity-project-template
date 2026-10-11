using System;
using System.Collections.Generic;
using Core.Logging;
using Core.Results;
using Core.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Core.Settings
{
    internal sealed class SettingsSectionStore
    {
        private Dictionary<string, JToken> _sections = new();

        private readonly IJsonSerializer _serializer;
        private readonly HashSet<string> _reportedCorruptSections = new();
        private readonly HashSet<string> _reportedNewerSections = new();
        private readonly Dictionary<string, JObject> _newerSectionSessionData = new();

        public SettingsSectionStore(IJsonSerializer serializer)
        {
            _serializer = serializer;
        }

        public T Read<T>(SettingsSection<T> section) where T : class
        {
            ValidateSection(section);

            if (!_sections.TryGetValue(section.Key, out var stored))
            {
                return section.Default;
            }

            if (TryGetNewerVersion(section, stored, out var newerVersion))
            {
                ReportNewerSection(section, newerVersion);
                return ReadSessionData(section);
            }

            if (ReadSection(section, stored).TryPickT0(out var data, out var corrupted))
            {
                return data;
            }

            if (_reportedCorruptSections.Add(section.Key))
            {
                Log.Warn(LogTags.Settings, $"Settings section '{section.Key}' is corrupted, using its defaults: {corrupted.Reason}");
            }

            return section.Default;
        }

        public void Write<T>(SettingsSection<T> section, T data) where T : class
        {
            ValidateSection(section);

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!_serializer.Deserialize<JObject>(_serializer.Serialize(data)).TryPickT0(out var serialized, out _))
            {
                throw new ArgumentException($"Data of settings section '{section.Key}' must serialize to a JSON object, but {typeof(T).Name} does not.", nameof(data));
            }

            if (_sections.TryGetValue(section.Key, out var stored) && TryGetNewerVersion(section, stored, out var newerVersion))
            {
                ReportNewerSection(section, newerVersion);
                _newerSectionSessionData[section.Key] = serialized;
                return;
            }

            _sections[section.Key] = CreateSectionEnvelope(section.CurrentVersion, serialized);
            _reportedCorruptSections.Remove(section.Key);
        }

        public void Replace(Dictionary<string, JToken> sections)
        {
            _sections = sections;
            _reportedCorruptSections.Clear();
            _reportedNewerSections.Clear();
            _newerSectionSessionData.Clear();
        }

        public Dictionary<string, JToken> Snapshot()
        {
            return new Dictionary<string, JToken>(_sections);
        }

        private static void ValidateSection<T>(SettingsSection<T> section) where T : class
        {
            if (string.IsNullOrWhiteSpace(section.Key))
            {
                throw new ArgumentException("Settings section key cannot be empty.", nameof(section));
            }

            if (section.CurrentVersion < 1)
            {
                throw new ArgumentException($"Settings section '{section.Key}' must have a version of at least 1, but has {section.CurrentVersion}.", nameof(section));
            }

            if (section.Default is null)
            {
                throw new ArgumentException($"Settings section '{section.Key}' must have a default value.", nameof(section));
            }
        }

        private OneOf<T, Corrupted> ReadSection<T>(SettingsSection<T> section, JToken stored) where T : class
        {
            if (stored is not JObject envelope
                || envelope["version"] is not JValue { Value: long version and >= 1 and <= int.MaxValue }
                || envelope["data"] is not JObject data)
            {
                return new Corrupted("It has no data or an invalid version.");
            }

            var isMigrated = version < section.CurrentVersion;

            if (isMigrated)
            {
                if (!section.Migrate((JObject)data.DeepClone(), (int)version).TryPickT0(out data, out var migrationCorrupted))
                {
                    return migrationCorrupted;
                }
            }

            if (!Deserialize(section, data).TryPickT0(out var value, out var corrupted))
            {
                return corrupted;
            }

            if (isMigrated)
            {
                _sections[section.Key] = CreateSectionEnvelope(section.CurrentVersion, data);
            }

            return value;
        }

        private T ReadSessionData<T>(SettingsSection<T> section) where T : class
        {
            if (!_newerSectionSessionData.TryGetValue(section.Key, out var sessionData))
            {
                return section.Default;
            }

            return Deserialize(section, sessionData).Match(value => value, _ => section.Default);
        }

        private OneOf<T, Corrupted> Deserialize<T>(SettingsSection<T> section, JObject data) where T : class
        {
            return _serializer.Deserialize<T>(data.ToString(Formatting.None)).Match<OneOf<T, Corrupted>>(
                value => value,
                corrupted => new Corrupted($"It does not match {typeof(T).Name}: {corrupted.Reason}"));
        }

        private static bool TryGetNewerVersion<T>(SettingsSection<T> section, JToken stored, out long version) where T : class
        {
            version = stored is JObject envelope && envelope["version"] is JValue { Value: long storedVersion } ? storedVersion : 0;
            return version > section.CurrentVersion;
        }

        private void ReportNewerSection<T>(SettingsSection<T> section, long version) where T : class
        {
            if (_reportedNewerSections.Add(section.Key))
            {
                Log.Warn(LogTags.Settings, $"Settings section '{section.Key}' was written by a newer game version (version {version}, supported {section.CurrentVersion}). It is kept unchanged; this session uses its defaults and does not save changes to it.");
            }
        }

        private static JObject CreateSectionEnvelope(int version, JObject data)
        {
            return new JObject
            {
                ["version"] = version,
                ["data"] = data,
            };
        }
    }
}
