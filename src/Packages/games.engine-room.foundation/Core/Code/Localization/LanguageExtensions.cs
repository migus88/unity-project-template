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
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Language has no culture."),
            };
        }

        public static string GetNativeName(this Language language)
        {
            return language switch
            {
                Language.English => "English",
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Language has no native name."),
            };
        }
    }
}
