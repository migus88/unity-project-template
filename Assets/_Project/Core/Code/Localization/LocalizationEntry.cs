using System;
using UnityEngine;

namespace Core.Localization
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string Key => _key;

        [SerializeField] private string _key = string.Empty;
        [SerializeField, TextArea] private string _english = string.Empty;
        [SerializeField, TextArea] private string _polish = string.Empty;

        public string GetText(Language language)
        {
            return language switch
            {
                Language.English => _english,
                Language.Polish => _polish,
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Localization tables have no column for this language."),
            };
        }
    }
}
