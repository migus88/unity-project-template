using System;
using System.Globalization;

namespace Core.Localization
{
    public static class LanguageExtensions
    {
        public static CultureInfo GetCulture(this Language language)
        {
            return language switch
            {
                Language.English => CultureInfo.GetCultureInfo("en"),
                Language.Polish => CultureInfo.GetCultureInfo("pl"),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Language has no culture."),
            };
        }

        public static string GetNativeName(this Language language)
        {
            return language switch
            {
                Language.English => "English",
                Language.Polish => "Polski",
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Language has no native name."),
            };
        }
    }
}
