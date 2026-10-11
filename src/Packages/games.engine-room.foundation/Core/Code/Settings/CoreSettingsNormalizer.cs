using System;
using System.Collections.Generic;
using Core.Localization;
using UnityEngine;

namespace Core.Settings
{
    internal sealed class CoreSettingsNormalizer
    {
        private readonly IGraphicsDevice _graphics;
        private readonly SettingsDefaults _defaults;
        private readonly HashSet<Language> _supportedLanguages;

        public CoreSettingsNormalizer(IGraphicsDevice graphics, SettingsDefaults defaults, IReadOnlyCollection<Language> supportedLanguages)
        {
            _graphics = graphics;
            _defaults = defaults;
            _supportedLanguages = new HashSet<Language>(supportedLanguages);
            ValidateDefaults(defaults);
        }

        public SettingsState CreateDefaultState()
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

        public SettingsState ToState(CoreSettingsDto dto, Normalization normalization)
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

        public static CoreSettingsDto ToDto(SettingsState state)
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

        public void Validate(SettingsState state)
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

        internal sealed class Normalization
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
