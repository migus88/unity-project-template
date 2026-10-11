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
using Newtonsoft.Json.Linq;
using OneOf;
using R3;
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

        private bool _isSessionOnly;

        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private readonly IAudioService _audio;
        private readonly ILocalizationService _localization;
        private readonly IInputService _input;
        private readonly IGraphicsDevice _graphics;
        private readonly CoreSettingsNormalizer _normalizer;
        private readonly SettingsSectionStore _sections;
        private readonly ReactiveProperty<SettingsState> _current;
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
            _normalizer = new CoreSettingsNormalizer(graphics, defaults, supportedLanguages);
            _sections = new SettingsSectionStore(serializer);
            _current = new ReactiveProperty<SettingsState>(_normalizer.CreateDefaultState());
        }

        public async UniTask LoadAsync(CancellationToken ct)
        {
            _isSessionOnly = false;
            var read = await _storage.ReadAsync(FilePath, ct);

            if (read.TryPickT1(out _, out var readRemainder))
            {
                Log.Info(LogTags.Settings, "No settings file yet, using defaults.");
                ApplyEffects(_normalizer.CreateDefaultState());
                await SaveAndLogAsync(ct);
                return;
            }

            if (readRemainder.TryPickT1(out var readError, out var content))
            {
                Log.Warn(LogTags.Settings, $"Settings could not be read, using defaults without overwriting the file: {readError.Message}");
                ApplyEffects(_normalizer.CreateDefaultState());
                return;
            }

            var parsed = Parse(content);

            if (parsed.TryPickT1(out var newerFormat, out var parsedRemainder))
            {
                _isSessionOnly = true;
                Log.Warn(LogTags.Settings, $"Settings file is kept unchanged because it was written by a newer game version (format version {newerFormat.Version}, supported {CurrentFormatVersion}). Using defaults; changes last for this session only.");
                ApplyEffects(_normalizer.CreateDefaultState());
                return;
            }

            if (parsedRemainder.TryPickT1(out var corrupted, out var file))
            {
                Log.Warn(LogTags.Settings, $"Settings file is corrupted, using defaults: {corrupted.Reason}");
                ApplyEffects(_normalizer.CreateDefaultState());
                await SaveAndLogAsync(ct);
                return;
            }

            _sections.Replace(file.Sections);

            var normalization = new CoreSettingsNormalizer.Normalization();
            var state = _normalizer.ToState(file.Core, normalization);

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
            _normalizer.Validate(state);

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
            return _sections.Read(section);
        }

        public void Write<T>(SettingsSection<T> section, T data) where T : class
        {
            _sections.Write(section, data);
        }

        public async UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            await _saveGate.WaitAsync(ct);

            try
            {
                if (_isSessionOnly)
                {
                    return new Success();
                }

                var file = new SettingsFileDto(CurrentFormatVersion, CoreSettingsNormalizer.ToDto(_current.Value), _sections.Snapshot());
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

        private OneOf<SettingsFile, NewerFormat, Corrupted> Parse(string content)
        {
            if (!_serializer.Deserialize<JObject>(content).TryPickT0(out var root, out var rootCorrupted))
            {
                return rootCorrupted;
            }

            if (root["formatVersion"] is JValue { Value: long formatVersion } && formatVersion > CurrentFormatVersion)
            {
                return new NewerFormat(formatVersion);
            }

            if (!_serializer.Deserialize<SettingsFileDto>(content).TryPickT0(out var file, out var corrupted))
            {
                return corrupted;
            }

            return file.FormatVersion switch
            {
                CurrentFormatVersion => new SettingsFile(file.Core ?? EmptyCoreDto, CollectSections(file.Sections), IsFromOlderFormat: false),
                FlatFormatVersion => ParseFlatFormat(content).Match<OneOf<SettingsFile, NewerFormat, Corrupted>>(flat => flat, flatCorrupted => flatCorrupted),
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

        public void Dispose()
        {
            _current.Dispose();
        }

        private sealed record SettingsFile(CoreSettingsDto Core, Dictionary<string, JToken> Sections, bool IsFromOlderFormat);

        private readonly record struct NewerFormat(long Version);
    }
}
