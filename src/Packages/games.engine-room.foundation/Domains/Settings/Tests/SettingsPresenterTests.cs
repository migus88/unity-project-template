using System.Linq;
using AwesomeAssertions;
using Core;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Core.Settings;
using NSubstitute;
using NUnit.Framework;
using R3;
using Settings.Analytics;
using TestUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Settings.Tests
{
    public sealed class SettingsPresenterTests
    {
        private const string ScenePath = "Packages/games.engine-room.foundation/Domains/Settings/Scenes/Settings.unity";

        private Scene _scene;
        private SettingsView _view = null!;
        private CoreConfig _coreConfig = null!;
        private ReactiveProperty<SettingsState> _state = null!;
        private ISettingsService _settings = null!;
        private FakeAnalytics _analytics = null!;
        private SettingsPresenter? _presenter;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SceneManager.GetSceneByPath(ScenePath).isLoaded.Should().BeFalse($"{ScenePath} must be closed before its tests run");
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _view = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SettingsView>(true)).Single();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _coreConfig = ScriptableObject.CreateInstance<CoreConfig>();
            _state = new ReactiveProperty<SettingsState>(new SettingsState(1f, 1f, 1f, 1f, Language.English, 0, FullScreenMode.Windowed, new Resolution(), false, string.Empty));
            _settings = Substitute.For<ISettingsService>();
            _settings.Current.Returns(_state);
            _analytics = new FakeAnalytics();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _presenter = null;
            _state.Dispose();
            Object.DestroyImmediate(_coreConfig);
        }

        [Test]
        public void Start_WithFrom_TracksSettingsOpenedWithThatOrigin()
        {
            // Arrange
            _presenter = new SettingsPresenter(_view, _settings, Substitute.For<IInputService>(), _coreConfig, new DomainCompletion<SettingsResult>(), _analytics, new SettingsArgs { From = "pause" });

            // Act
            _presenter.Start();

            // Assert
            _analytics.OfType<SettingsOpenedEvent>().Should().ContainSingle().Which.From.Should().Be("pause");
        }
    }
}
