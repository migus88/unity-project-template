using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Logging;
using R3;

namespace Core.Localization
{
    public sealed class LocalizationService : ILocalizationService, IDisposable
    {
        public ReadOnlyReactiveProperty<Language> Current => _current;

        private CultureInfo _culture;

        private readonly Language _defaultLanguage;
        private readonly HashSet<Language> _supportedLanguages;
        private readonly ReactiveProperty<Language> _current;
        private readonly Dictionary<string, RegisteredTable> _tables = new(StringComparer.Ordinal);
        private readonly HashSet<TextKey> _reportedMissingKeys = new();
        private readonly HashSet<(TextKey Key, Language Language)> _reportedMissingTexts = new();

        public LocalizationService(Language defaultLanguage, IReadOnlyCollection<Language> supportedLanguages)
        {
            _supportedLanguages = new HashSet<Language>(supportedLanguages);

            if (_supportedLanguages.Contains(Language.None))
            {
                throw new ArgumentException($"{Language.None} cannot be a supported language.", nameof(supportedLanguages));
            }

            if (!_supportedLanguages.Contains(defaultLanguage))
            {
                throw new ArgumentException($"Default language {defaultLanguage} is not supported.", nameof(defaultLanguage));
            }

            _defaultLanguage = defaultLanguage;
            _culture = defaultLanguage.GetCulture();
            _current = new ReactiveProperty<Language>(defaultLanguage);
        }

        public string Get(TextKey key)
        {
            if (!TryFindEntry(key, out var entry))
            {
                if (_reportedMissingKeys.Add(key))
                {
                    Log.Warn(LogTags.Localization, $"Missing text '{key}'.");
                }

                return key.ToString();
            }

            var language = _current.Value;
            var text = entry.GetText(language);

            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            var fallback = language == _defaultLanguage ? string.Empty : entry.GetText(_defaultLanguage);

            if (_reportedMissingTexts.Add((key, language)))
            {
                var fallbackDescription = string.IsNullOrEmpty(fallback) ? "the key" : _defaultLanguage.ToString();
                Log.Warn(LogTags.Localization, $"Text '{key}' has no {language} translation. Using {fallbackDescription} instead.");
            }

            return string.IsNullOrEmpty(fallback) ? key.ToString() : fallback;
        }

        public string Format(TextKey key, params object[] args)
        {
            return string.Format(_culture, Get(key), args);
        }

        public void SetLanguage(Language language)
        {
            if (!_supportedLanguages.Contains(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "Language is not supported.");
            }

            if (language == _current.Value)
            {
                return;
            }

            _culture = language.GetCulture();
            _current.Value = language;
            Log.Info(LogTags.Localization, $"Language set to {language}.");
        }

        internal void AddTable(LocalizationTable table)
        {
            if (string.IsNullOrEmpty(table.TableName))
            {
                throw new ArgumentException($"Localization table '{table.name}' has no table name.", nameof(table));
            }

            if (_tables.ContainsKey(table.TableName))
            {
                throw new InvalidOperationException($"A localization table named '{table.TableName}' is already registered.");
            }

            var entries = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

            foreach (var entry in table.Entries)
            {
                if (!entries.TryAdd(entry.Key, entry))
                {
                    throw new ArgumentException($"Localization table '{table.TableName}' contains the key '{entry.Key}' more than once.", nameof(table));
                }
            }

            _tables.Add(table.TableName, new RegisteredTable(table, entries));
        }

        internal void RemoveTable(LocalizationTable table)
        {
            if (!_tables.TryGetValue(table.TableName, out var registered) || registered.Table != table)
            {
                throw new InvalidOperationException($"Localization table '{table.TableName}' is not registered.");
            }

            _tables.Remove(table.TableName);
        }

        private bool TryFindEntry(TextKey key, [NotNullWhen(true)] out LocalizationEntry? entry)
        {
            entry = null;
            return _tables.TryGetValue(key.Table, out var table) && table.Entries.TryGetValue(key.Key, out entry);
        }

        public void Dispose()
        {
            _current.Dispose();
        }

        private sealed record RegisteredTable(LocalizationTable Table, Dictionary<string, LocalizationEntry> Entries);
    }
}
