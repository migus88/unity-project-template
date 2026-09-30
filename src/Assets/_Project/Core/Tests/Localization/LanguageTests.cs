using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Core.Localization;
using NUnit.Framework;

namespace Core.Tests.Localization
{
    public sealed class LanguageTests
    {
        private static IEnumerable<Language> Languages => Enum.GetValues(typeof(Language)).Cast<Language>().Where(language => language != Language.None);

        [TestCaseSource(nameof(Languages))]
        public void GetText_EveryLanguage_HasTableColumn(Language language)
        {
            // Arrange
            var entry = new LocalizationEntry();

            // Act
            Action act = () => entry.GetText(language);

            // Assert
            act.Should().NotThrow();
        }

        [TestCaseSource(nameof(Languages))]
        public void GetCultureAndNativeName_EveryLanguage_AreDefined(Language language)
        {
            // Act
            var culture = language.GetCulture();
            var nativeName = language.GetNativeName();

            // Assert
            culture.Should().NotBeNull();
            nativeName.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void GetText_None_Throws()
        {
            // Arrange
            var entry = new LocalizationEntry();

            // Act
            Action act = () => entry.GetText(Language.None);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
