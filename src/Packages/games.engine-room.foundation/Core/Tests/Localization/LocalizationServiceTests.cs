using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Localization;
using NUnit.Framework;
using R3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Localization
{
    public sealed class LocalizationServiceTests
    {
        private static readonly TextKey PlayKey = new("Shared", "play");
        private static readonly TextKey QuitKey = new("Shared", "quit");
        private static readonly TextKey BackKey = new("Shared", "back");
        private static readonly TextKey ScoreKey = new("Shared", "score");

        private LocalizationTable _table = null!;
        private LocalizationService _service = null!;
        private List<string> _warnings = null!;

        [SetUp]
        public void SetUp()
        {
            _table = TestLocalizationTables.Create(
                "Shared",
                ("play", "Play", "Graj"),
                ("quit", "Quit", ""),
                ("back", "", ""),
                ("score", "Score: {0:N1}", "Wynik: {0:N1}"));
            _service = new LocalizationService(Language.English, [Language.English, Language.Polish]);
            _service.AddTable(_table);
            _warnings = new List<string>();
            Application.logMessageReceived += OnLogMessageReceived;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            _service.Dispose();
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void Constructor_DefaultLanguageNotSupported_Throws()
        {
            // Act
            Action act = () => new LocalizationService(Language.Polish, [Language.English]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Constructor_NoneSupported_Throws()
        {
            // Act
            Action act = () => new LocalizationService(Language.English, [Language.English, Language.None]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Current_Initially_IsDefaultLanguage()
        {
            // Act
            var current = _service.Current.CurrentValue;

            // Assert
            current.Should().Be(Language.English);
        }

        [Test]
        public void Get_KeyTranslated_ReturnsTextInCurrentLanguage()
        {
            // Act
            var text = _service.Get(PlayKey);

            // Assert
            text.Should().Be("Play");
            _warnings.Should().BeEmpty();
        }

        [Test]
        public void Get_AfterSetLanguage_ReturnsTextInNewLanguage()
        {
            // Arrange
            _service.SetLanguage(Language.Polish);

            // Act
            var text = _service.Get(PlayKey);

            // Assert
            text.Should().Be("Graj");
        }

        [Test]
        public void Get_TranslationMissing_FallsBackToDefaultLanguageAndWarnsOnce()
        {
            // Arrange
            _service.SetLanguage(Language.Polish);

            // Act
            var first = _service.Get(QuitKey);
            var second = _service.Get(QuitKey);

            // Assert
            first.Should().Be("Quit");
            second.Should().Be("Quit");
            _warnings.Should().ContainSingle().Which.Should().Contain("Shared/quit");
        }

        [Test]
        public void Get_TextMissingInEveryLanguage_ReturnsKeyPath()
        {
            // Act
            var text = _service.Get(BackKey);

            // Assert
            text.Should().Be("Shared/back");
            _warnings.Should().ContainSingle();
        }

        [Test]
        public void Get_KeyMissing_ReturnsKeyPathAndWarnsOnce()
        {
            // Arrange
            var missingKey = new TextKey("Shared", "missing");

            // Act
            var first = _service.Get(missingKey);
            var second = _service.Get(missingKey);

            // Assert
            first.Should().Be("Shared/missing");
            second.Should().Be("Shared/missing");
            _warnings.Should().ContainSingle().Which.Should().Contain("Shared/missing");
        }

        [Test]
        public void Get_TableNotRegistered_ReturnsKeyPath()
        {
            // Act
            var text = _service.Get(new TextKey("Gameplay", "win_title"));

            // Assert
            text.Should().Be("Gameplay/win_title");
        }

        [Test]
        public void Get_TableRemoved_ReturnsKeyPath()
        {
            // Arrange
            _service.RemoveTable(_table);

            // Act
            var text = _service.Get(PlayKey);

            // Assert
            text.Should().Be("Shared/play");
        }

        [Test]
        public void Get_DefaultKey_ReturnsSlash()
        {
            // Act
            var text = _service.Get(default);

            // Assert
            text.Should().Be("/");
        }

        [Test]
        public void Format_Args_FormatsWithCurrentLanguageCulture()
        {
            // Act
            var english = _service.Format(ScoreKey, 1.5);
            _service.SetLanguage(Language.Polish);
            var polish = _service.Format(ScoreKey, 1.5);

            // Assert
            english.Should().Be("Score: 1.5");
            polish.Should().Be("Wynik: 1,5");
        }

        [Test]
        public void SetLanguage_NewLanguage_NotifiesSubscribers()
        {
            // Arrange
            var values = new List<Language>();
            using var subscription = _service.Current.Subscribe(values.Add);

            // Act
            _service.SetLanguage(Language.Polish);

            // Assert
            _service.Current.CurrentValue.Should().Be(Language.Polish);
            values.Should().Equal(Language.English, Language.Polish);
        }

        [Test]
        public void SetLanguage_SameLanguage_DoesNotNotify()
        {
            // Arrange
            var values = new List<Language>();
            using var subscription = _service.Current.Subscribe(values.Add);

            // Act
            _service.SetLanguage(Language.English);

            // Assert
            values.Should().Equal(Language.English);
        }

        [TestCase(Language.None)]
        [TestCase((Language)99)]
        public void SetLanguage_Unsupported_Throws(Language language)
        {
            // Act
            Action act = () => _service.SetLanguage(language);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _service.Current.CurrentValue.Should().Be(Language.English);
        }

        [Test]
        public void AddTable_SameTableNameTwice_Throws()
        {
            // Arrange
            var other = TestLocalizationTables.Create("Shared", ("other", "Other", "Inny"));

            try
            {
                // Act
                Action act = () => _service.AddTable(other);

                // Assert
                act.Should().Throw<InvalidOperationException>();
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AddTable_DuplicateKey_Throws()
        {
            // Arrange
            var duplicated = TestLocalizationTables.Create("Duplicated", ("play", "Play", "Graj"), ("play", "Play", "Graj"));

            try
            {
                // Act
                Action act = () => _service.AddTable(duplicated);

                // Assert
                act.Should().Throw<ArgumentException>();
            }
            finally
            {
                Object.DestroyImmediate(duplicated);
            }
        }

        [Test]
        public void AddTable_NoTableName_Throws()
        {
            // Arrange
            var unnamed = TestLocalizationTables.Create(string.Empty);

            try
            {
                // Act
                Action act = () => _service.AddTable(unnamed);

                // Assert
                act.Should().Throw<ArgumentException>();
            }
            finally
            {
                Object.DestroyImmediate(unnamed);
            }
        }

        [Test]
        public void RemoveTable_NotRegistered_Throws()
        {
            // Arrange
            _service.RemoveTable(_table);

            // Act
            Action act = () => _service.RemoveTable(_table);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        private void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                _warnings.Add(message);
            }
        }
    }
}
