using System;
using Core.Localization;
using UnityEngine;
using UnityEngine.Audio;

namespace Core
{
    [CreateAssetMenu(menuName = "Core/Core Config")]
    public sealed class CoreConfig : ScriptableObject
    {
        [field: SerializeField] public AudioMixer AudioMixer { get; private set; } = null!;
        [field: SerializeField] public Language DefaultLanguage { get; private set; } = Language.English;
        [field: SerializeField] public Language[] SupportedLanguages { get; private set; } = [Language.English, Language.Polish];
        [field: SerializeField] public LocalizationTable SharedText { get; private set; } = null!;
        [field: SerializeField, Range(0f, 1f)] public float DefaultMasterVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultMusicVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultSfxVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float DefaultUiVolume { get; private set; } = 1f;

        private void OnValidate()
        {
            if (Array.IndexOf(SupportedLanguages, DefaultLanguage) < 0)
            {
                Debug.LogError("The default language must be one of the supported languages.", this);
            }
        }
    }
}
