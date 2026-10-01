using R3;

namespace Core.Localization
{
    public interface ILocalizationService
    {
        ReadOnlyReactiveProperty<Language> Current { get; }

        string Get(TextKey key);
        string Format(TextKey key, params object[] args);
        void SetLanguage(Language language);
    }
}
