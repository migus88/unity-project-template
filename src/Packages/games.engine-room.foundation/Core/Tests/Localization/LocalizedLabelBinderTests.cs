using System.Collections.Generic;
using AwesomeAssertions;
using Core.Localization;
using NSubstitute;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Core.Tests.Localization
{
    public sealed class LocalizedLabelBinderTests
    {
        private static readonly TextKey PlayKey = new("Shared", "play");
        private static readonly TextKey QuitKey = new("Shared", "quit");

        private Dictionary<TextKey, string> _texts = null!;
        private ReactiveProperty<Language> _language = null!;
        private ILocalizationService _localization = null!;
        private Subject<Scene> _loadedScenes = null!;
        private GameObject _root = null!;
        private LocalizedLabelBinder _binder = null!;

        [SetUp]
        public void SetUp()
        {
            _texts = new Dictionary<TextKey, string> { [PlayKey] = "Play", [QuitKey] = "Quit" };
            _language = new ReactiveProperty<Language>(Language.English);
            _localization = Substitute.For<ILocalizationService>();
            _localization.Current.Returns(_language);
            _localization.Get(Arg.Any<TextKey>()).Returns(call => _texts[call.Arg<TextKey>()]);
            _loadedScenes = new Subject<Scene>();
            _root = new GameObject("Root");
            _binder = new LocalizedLabelBinder(_localization, [_root], _loadedScenes);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _loadedScenes.Dispose();
            _language.Dispose();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Start_LabelsUnderRoots_SetsTheirText()
        {
            // Arrange
            var play = TestLocalizationTables.CreateLabel(_root.transform, PlayKey);
            var quit = TestLocalizationTables.CreateLabel(play.transform, QuitKey);

            // Act
            _binder.Start();

            // Assert
            TextOf(play).Should().Be("Play");
            TextOf(quit).Should().Be("Quit");
        }

        [Test]
        public void LanguageChanged_LabelDestroyed_UpdatesRemainingLabels()
        {
            // Arrange
            var play = TestLocalizationTables.CreateLabel(_root.transform, PlayKey);
            var quit = TestLocalizationTables.CreateLabel(_root.transform, QuitKey);
            _binder.Start();
            Object.DestroyImmediate(play.gameObject);

            // Act
            ChangeLanguage();

            // Assert
            TextOf(quit).Should().Be("Quit (changed)");
        }

        [Test]
        public void SceneLoaded_LabelsInScene_SetsTheirTextAndUpdatesThemOnLanguageChange()
        {
            // Arrange
            _binder.Start();
            var sceneRoot = new GameObject("SceneRoot");

            try
            {
                var quit = TestLocalizationTables.CreateLabel(sceneRoot.transform, QuitKey);

                // Act
                _loadedScenes.OnNext(sceneRoot.scene);
                var textAfterLoad = TextOf(quit);
                ChangeLanguage();

                // Assert
                textAfterLoad.Should().Be("Quit");
                TextOf(quit).Should().Be("Quit (changed)");
            }
            finally
            {
                Object.DestroyImmediate(sceneRoot);
            }
        }

        [Test]
        public void Dispose_AfterStart_StopsUpdatingLabels()
        {
            // Arrange
            var play = TestLocalizationTables.CreateLabel(_root.transform, PlayKey);
            _binder.Start();

            // Act
            _binder.Dispose();
            ChangeLanguage();

            // Assert
            TextOf(play).Should().Be("Play");
        }

        private void ChangeLanguage()
        {
            foreach (var key in new List<TextKey>(_texts.Keys))
            {
                _texts[key] += " (changed)";
            }

            _language.ForceNotify();
        }

        private static string TextOf(LocalizedLabel label)
        {
            return label.GetComponent<TMP_Text>().text;
        }
    }
}
