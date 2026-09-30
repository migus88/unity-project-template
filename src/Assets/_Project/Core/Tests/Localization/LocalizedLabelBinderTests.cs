using AwesomeAssertions;
using Core.Localization;
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

        private LocalizationTable _table = null!;
        private LocalizationService _service = null!;
        private Subject<Scene> _loadedScenes = null!;
        private GameObject _root = null!;
        private LocalizedLabelBinder _binder = null!;

        [SetUp]
        public void SetUp()
        {
            _table = TestLocalizationTables.Create("Shared", ("play", "Play", "Graj"), ("quit", "Quit", "Wyjdź"));
            _service = new LocalizationService(Language.English, [Language.English, Language.Polish]);
            _service.AddTable(_table);
            _loadedScenes = new Subject<Scene>();
            _root = new GameObject("Root");
            _binder = new LocalizedLabelBinder(_service, [_root], _loadedScenes);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _loadedScenes.Dispose();
            _service.Dispose();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_table);
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
        public void SetLanguage_AfterStart_UpdatesLabels()
        {
            // Arrange
            var play = TestLocalizationTables.CreateLabel(_root.transform, PlayKey);
            _binder.Start();

            // Act
            _service.SetLanguage(Language.Polish);

            // Assert
            TextOf(play).Should().Be("Graj");
        }

        [Test]
        public void SetLanguage_LabelDestroyed_UpdatesRemainingLabels()
        {
            // Arrange
            var play = TestLocalizationTables.CreateLabel(_root.transform, PlayKey);
            var quit = TestLocalizationTables.CreateLabel(_root.transform, QuitKey);
            _binder.Start();
            Object.DestroyImmediate(play.gameObject);

            // Act
            _service.SetLanguage(Language.Polish);

            // Assert
            TextOf(quit).Should().Be("Wyjdź");
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
                _service.SetLanguage(Language.Polish);

                // Assert
                textAfterLoad.Should().Be("Quit");
                TextOf(quit).Should().Be("Wyjdź");
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
            _service.SetLanguage(Language.Polish);

            // Assert
            TextOf(play).Should().Be("Play");
        }

        private static string TextOf(LocalizedLabel label)
        {
            return label.GetComponent<TMP_Text>().text;
        }
    }
}
