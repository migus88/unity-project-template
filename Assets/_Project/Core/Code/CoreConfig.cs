using System;
using Core.Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Core
{
    [CreateAssetMenu(menuName = "Core/Core Config")]
    public sealed class CoreConfig : ScriptableObject
    {
        [field: SerializeField, Required] public AudioMixer AudioMixer { get; private set; } = null!;
        [field: SerializeField, MinValue(0)] public int AudioSourcePoolSize { get; private set; } = 16;
        [field: SerializeField, ValidateInput(nameof(IsDefaultLanguageSupported), "The default language must be one of the supported languages.")] public Language DefaultLanguage { get; private set; } = Language.English;
        [field: SerializeField, Required] public Language[] SupportedLanguages { get; private set; } = [Language.English, Language.Polish];
        [field: SerializeField, Required] public LocalizationTable SharedText { get; private set; } = null!;
        [field: SerializeField, Range(0f, 1f)] public float DefaultMasterVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultMusicVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultSfxVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultUiVolume { get; private set; } = 1f;

        private bool IsDefaultLanguageSupported(Language language)
        {
            return Array.IndexOf(SupportedLanguages, language) >= 0;
        }
    }
}
