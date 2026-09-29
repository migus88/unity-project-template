using System;
using System.Collections.Generic;
using System.Threading;
using Core.Audio;
using Core.Input;
using Core.Localization;
using Core.Logging;
using Core.Results;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using R3;
using UnityEngine;
using Success = OneOf.Types.Success;

namespace Core.Settings
{
    public sealed class SettingsService : ISettingsService, ISettingsLoader, IDisposable
    {
        public const string FilePath = "settings.json";
        public const int CurrentFormatVersion = 2;

        private const int FlatFormatVersion = 1;

        private static readonly CoreSettingsDto EmptyCoreDto = new(null, null, null, null, null, null, null, null, null, null, null, null, null);

        public ReadOnlyReactiveProperty<SettingsState> Current => _current;

        private Dictionary<string, JToken> _sections = new();

        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private readonly IAudioService _audio;
        private readonly ILocalizationService _localization;
        private readonly IInputService _input;
        private readonly IGraphicsDevice _graphics;
        private readonly SettingsDefaults _defaults;
        private readonly HashSet<Language> _supportedLanguages;
        private readonly ReactiveProperty<SettingsState> _current;
        private readonly HashSet<string> _reportedCorruptSections = new();
        private readonly SemaphoreSlim _saveGate = new(1, 1);

        public SettingsService(
            IFileStorage storage,
            IJsonSerializer serializer,
            IAudioService audio,
            ILocalizationService localization,
            IInputService input,
            IGraphicsDevice graphics,
            SettingsDefaults defaults,
            IReadOnlyCollection<Language> supportedLanguages)
        {
            _storage = storage;
            _serializer = serializer;
            _audio = audio;
            _localization = localization;
            _input = input;
            _graphics = graphics;
            _defaults = defaults;
            _supportedLanguages = new HashSet<Language>(supportedLanguages);
            ValidateDefaults(defaults);
            _current = new ReactiveProperty<SettingsState>(CreateDefaultState());
        }

        public async UniTask LoadAsync(CancellationToken ct)
        {
            var read = await _storage.ReadAsync(FilePath, ct);

            if (read.TryPickT1(out _, out var readRemainder))
            {
                Log.Info(LogTags.Settings, "No settings file yet, using defaults.");
                ApplyEffects(CreateDefaultState());
                await SaveAndLogAsync(ct);
                return;
            }

            if (readRemainder.TryPickT1(out var readError, out var content))
            {
                Log.Warn(LogTags.Settings, $"Settings could not be read, using defaults without overwriting the file: {readError.Message}");
                ApplyEffects(CreateDefaultState());
                return;
            }

            if (!Parse(content).TryPickT0(out var file, out var corrupted))
            {
                Log.Warn(LogTags.Settings, $"Settings file is corrupted, using defaults: {corrupted.Reason}");
                ApplyEffects(CreateDefaultState());
                await SaveAndLogAsync(ct);
                return;
            }

            _sections = file.Sections;
            _reportedCorruptSections.Clear();

            var normalization = new Normalization();
            var state = ToState(file.Core, normalization);

            if (InputBindingOverrides.Load(_input.Actions, state.BindingOverridesJson).TryPickT1(out _, out _))
            {
                normalization.AddInvalid("bindingOverridesJson");
                state = state with { BindingOverridesJson = string.Empty };
            }

            ApplyEffects(state);

            if (normalization.InvalidFields.Count > 0)
            {
                Log.Warn(LogTags.Settings, $"Settings file has invalid values for {string.Join(", ", normalization.InvalidFields)}, using defaults for them.");
            }

            if (file.IsFromOlderFormat)
            {
                Log.Info(LogTags.Settings, $"Settings file upgraded from format version {FlatFormatVersion} to {CurrentFormatVersion}.");
            }

            if (normalization.InvalidFields.Count > 0 || normalization.IsIncomplete || file.IsFromOlderFormat)
            {
                await SaveAndLogAsync(ct);
                return;
            }

            Log.Info(LogTags.Settings, "Settings loaded.");
        }

        public void Apply(SettingsState state)
        {
            Validate(state);

            if (string.Equals(state.BindingOverridesJson, _current.Value.BindingOverridesJson, StringComparison.Ordinal))
            {
                ApplyEffects(state);
                return;
            }

            if (InputBindingOverrides.Load(_input.Actions, state.BindingOverridesJson).TryPickT1(out var corrupted, out _))
            {
                InputBindingOverrides.Load(_input.Actions, _current.Value.BindingOverridesJson);
                throw new ArgumentException(corrupted.Reason, nameof(state));
            }

            ApplyEffects(state);
        }

        public T Read<T>(SettingsSection<T> section) where T : class
        {
            ValidateSection(section);

            if (!_sections.TryGetValue(section.Key, out var stored))
            {
                return section.Default;
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

            _sections[section.Key] = CreateSectionEnvelope(section.CurrentVersion, serialized);
            _reportedCorruptSections.Remove(section.Key);
        }

        public async UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            await _saveGate.WaitAsync(ct);

            try
            {
                var file = new SettingsFileDto(CurrentFormatVersion, ToDto(_current.Value), new Dictionary<string, JToken>(_sections));
                var json = _serializer.Serialize(file);
                return await _storage.WriteAsync(FilePath, json, ct);
            }
            finally
            {
                _saveGate.Release();
            }
        }

        private void ApplyEffects(SettingsState state)
        {
            _audio.SetVolume(AudioChannel.Master, state.MasterVolume);
            _audio.SetVolume(AudioChannel.Music, state.MusicVolume);
            _audio.SetVolume(AudioChannel.Sfx, state.SfxVolume);
            _audio.SetVolume(AudioChannel.Ui, state.UiVolume);
            _localization.SetLanguage(state.Language);
            _graphics.SetQualityLevel(state.QualityLevel);
            _graphics.SetVSync(state.VSync);
            _graphics.SetScreen(state.Resolution, state.FullScreenMode);
            _current.Value = state;
        }

        private async UniTask SaveAndLogAsync(CancellationToken ct)
        {
            var saved = await SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Settings, $"Settings could not be saved: {error.Message}");
            }
        }

        private OneOf<SettingsFile, Corrupted> Parse(string content)
        {
            if (!_serializer.Deserialize<SettingsFileDto>(content).TryPickT0(out var file, out var corrupted))
            {
                return corrupted;
            }

            return file.FormatVersion switch
            {
                CurrentFormatVersion => new SettingsFile(file.Core ?? EmptyCoreDto, CollectSections(file.Sections), IsFromOlderFormat: false),
                FlatFormatVersion => ParseFlatFormat(content),
                _ => new Corrupted($"Unsupported format version {file.FormatVersion}, expected {CurrentFormatVersion}."),
            };
        }

        private OneOf<SettingsFile, Corrupted> ParseFlatFormat(string content)
        {
            if (!_serializer.Deserialize<CoreSettingsDto>(content).TryPickT0(out var core, out var corrupted))
            {
                return corrupted;
            }

            return new SettingsFile(core, new Dictionary<string, JToken>(), IsFromOlderFormat: true);
        }

        private static Dictionary<string, JToken> CollectSections(Dictionary<string, JToken>? sections)
        {
            var collected = new Dictionary<string, JToken>();

            if (sections is null)
            {
                return collected;
            }

            foreach (var (key, section) in sections)
            {
                if (section is not null)
                {
                    collected[key] = section;
                }
            }

            return collected;
        }

        private OneOf<T, Corrupted> ReadSection<T>(SettingsSection<T> section, JToken stored) where T : class
        {
            if (stored is not JObject envelope
                || envelope["version"] is not JValue { Value: long version and >= 1 and <= int.MaxValue }
                || envelope["data"] is not JObject data)
            {
                return new Corrupted("It has no data or an invalid version.");
            }

            if (version > section.CurrentVersion)
            {
                return new Corrupted($"It has version {version}, newer than the supported version {section.CurrentVersion}.");
            }

            if (version < section.CurrentVersion)
            {
                if (!section.Migrate((JObject)data.DeepClone(), (int)version).TryPickT0(out data, out var migrationCorrupted))
                {
                    return migrationCorrupted;
                }

                _sections[section.Key] = CreateSectionEnvelope(section.CurrentVersion, data);
            }

            return _serializer.Deserialize<T>(data.ToString(Formatting.None)).Match<OneOf<T, Corrupted>>(
                value => value,
                corrupted => new Corrupted($"It does not match {typeof(T).Name}: {corrupted.Reason}"));
        }

        private static JObject CreateSectionEnvelope(int version, JObject data)
        {
            return new JObject
            {
                ["version"] = version,
                ["data"] = data,
            };
        }

        private SettingsState CreateDefaultState()
        {
            return new SettingsState(
                _defaults.MasterVolume,
                _defaults.MusicVolume,
                _defaults.SfxVolume,
                _defaults.UiVolume,
                _defaults.Language,
                _graphics.QualityLevel,
                _graphics.FullScreenMode,
                _graphics.Resolution,
                _graphics.VSync,
                string.Empty);
        }

        private SettingsState ToState(CoreSettingsDto dto, Normalization normalization)
        {
            var defaults = CreateDefaultState();

            return new SettingsState(
                ReadValue(dto.MasterVolume, defaults.MasterVolume, IsValidVolume, "masterVolume", normalization),
                ReadValue(dto.MusicVolume, defaults.MusicVolume, IsValidVolume, "musicVolume", normalization),
                ReadValue(dto.SfxVolume, defaults.SfxVolume, IsValidVolume, "sfxVolume", normalization),
                ReadValue(dto.UiVolume, defaults.UiVolume, IsValidVolume, "uiVolume", normalization),
                ReadEnum(dto.Language, defaults.Language, IsSupportedLanguage, "language", normalization),
                ReadValue(dto.QualityLevel, defaults.QualityLevel, IsValidQualityLevel, "qualityLevel", normalization),
                ReadEnum(dto.FullScreenMode, defaults.FullScreenMode, IsValidFullScreenMode, "fullScreenMode", normalization),
                ReadResolution(dto, defaults.Resolution, normalization),
                ReadValue(dto.VSync, defaults.VSync, _ => true, "vSync", normalization),
                ReadStringOrDefault(dto.BindingOverridesJson, defaults.BindingOverridesJson, normalization));
        }

        private static CoreSettingsDto ToDto(SettingsState state)
        {
            return new CoreSettingsDto(
                state.MasterVolume,
                state.MusicVolume,
                state.SfxVolume,
                state.UiVolume,
                state.Language.ToString(),
                state.QualityLevel,
                state.FullScreenMode.ToString(),
                state.Resolution.width,
                state.Resolution.height,
                state.Resolution.refreshRateRatio.numerator,
                state.Resolution.refreshRateRatio.denominator,
                state.VSync,
                state.BindingOverridesJson);
        }

        private static T ReadValue<T>(T? value, T fallback, Func<T, bool> isValid, string name, Normalization normalization) where T : struct
        {
            if (value is not { } present)
            {
                normalization.MarkIncomplete();
                return fallback;
            }

            if (!isValid(present))
            {
                normalization.AddInvalid(name);
                return fallback;
            }

            return present;
        }

        private static string ReadStringOrDefault(string? value, string fallback, Normalization normalization)
        {
            if (value is null)
            {
                normalization.MarkIncomplete();
                return fallback;
            }

            return value;
        }

        private static TEnum ReadEnum<TEnum>(string? value, TEnum fallback, Func<TEnum, bool> isValid, string name, Normalization normalization) where TEnum : struct, Enum
        {
            if (value is null)
            {
                normalization.MarkIncomplete();
                return fallback;
            }

            if (!Enum.TryParse<TEnum>(value, out var parsed) || !Enum.IsDefined(typeof(TEnum), parsed) || !isValid(parsed))
            {
                normalization.AddInvalid(name);
                return fallback;
            }

            return parsed;
        }

        private static Resolution ReadResolution(CoreSettingsDto dto, Resolution fallback, Normalization normalization)
        {
            if (dto.ResolutionWidth is null && dto.ResolutionHeight is null)
            {
                normalization.MarkIncomplete();
                return fallback;
            }

            if (dto.ResolutionWidth is not > 0 || dto.ResolutionHeight is not > 0)
            {
                normalization.AddInvalid("resolution");
                return fallback;
            }

            if (dto.RefreshRateNumerator is null || dto.RefreshRateDenominator is null)
            {
                normalization.MarkIncomplete();
            }

            return new Resolution
            {
                width = dto.ResolutionWidth.Value,
                height = dto.ResolutionHeight.Value,
                refreshRateRatio = new RefreshRate
                {
                    numerator = dto.RefreshRateNumerator ?? fallback.refreshRateRatio.numerator,
                    denominator = dto.RefreshRateDenominator ?? fallback.refreshRateRatio.denominator,
                },
            };
        }

        private void Validate(SettingsState state)
        {
            ValidateVolume(state.MasterVolume, nameof(SettingsState.MasterVolume));
            ValidateVolume(state.MusicVolume, nameof(SettingsState.MusicVolume));
            ValidateVolume(state.SfxVolume, nameof(SettingsState.SfxVolume));
            ValidateVolume(state.UiVolume, nameof(SettingsState.UiVolume));

            if (!IsSupportedLanguage(state.Language))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state.Language, "Language is not supported.");
            }

            if (!IsValidQualityLevel(state.QualityLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state.QualityLevel, $"Quality level must be between 0 and {_graphics.QualityLevelCount - 1}.");
            }

            if (!IsValidFullScreenMode(state.FullScreenMode))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state.FullScreenMode, "Full screen mode is not defined.");
            }

            if (state.Resolution.width <= 0 || state.Resolution.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(state), $"{state.Resolution.width}x{state.Resolution.height}", "Resolution must be positive.");
            }

            if (state.BindingOverridesJson is null)
            {
                throw new ArgumentNullException(nameof(state), "Binding overrides JSON cannot be null. Use an empty string for no overrides.");
            }
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

        private void ValidateDefaults(SettingsDefaults defaults)
        {
            if (!IsValidVolume(defaults.MasterVolume) || !IsValidVolume(defaults.MusicVolume) || !IsValidVolume(defaults.SfxVolume) || !IsValidVolume(defaults.UiVolume))
            {
                throw new ArgumentException("Default volumes must be between 0 and 1.", nameof(defaults));
            }

            if (!IsSupportedLanguage(defaults.Language))
            {
                throw new ArgumentException($"Default language {defaults.Language} is not supported.", nameof(defaults));
            }
        }

        private static void ValidateVolume(float volume, string name)
        {
            if (!IsValidVolume(volume))
            {
                throw new ArgumentOutOfRangeException("state", volume, $"{name} must be between 0 and 1.");
            }
        }

        private static bool IsValidVolume(float volume)
        {
            return volume is >= 0f and <= 1f;
        }

        private bool IsSupportedLanguage(Language language)
        {
            return _supportedLanguages.Contains(language);
        }

        private bool IsValidQualityLevel(int qualityLevel)
        {
            return qualityLevel >= 0 && qualityLevel < _graphics.QualityLevelCount;
        }

        private static bool IsValidFullScreenMode(FullScreenMode fullScreenMode)
        {
            return Enum.IsDefined(typeof(FullScreenMode), fullScreenMode);
        }

        public void Dispose()
        {
            _current.Dispose();
        }

        private sealed record SettingsFile(CoreSettingsDto Core, Dictionary<string, JToken> Sections, bool IsFromOlderFormat);

        private sealed class Normalization
        {
            public List<string> InvalidFields { get; } = new();
            public bool IsIncomplete { get; private set; }

            public void AddInvalid(string field)
            {
                InvalidFields.Add(field);
            }

            public void MarkIncomplete()
            {
                IsIncomplete = true;
            }
        }
    }
}
