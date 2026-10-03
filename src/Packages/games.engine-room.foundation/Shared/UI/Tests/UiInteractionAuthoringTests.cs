using System.Linq;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shared.UI.Tests
{
    public sealed class UiInteractionAuthoringTests
    {
        private const string Root = "Packages/games.engine-room.foundation/";
        private const string SettingsScenePath = Root + "Domains/Settings/Scenes/Settings.unity";

        private Scene _scene;

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void ButtonPrefab_Root_HasClickEmitter()
        {
            // Act
            var emitter = LoadPrefab("Shared/UI/Prefabs/Button.prefab").GetComponent<UiInteractionEmitter>();

            // Assert
            emitter.Should().NotBeNull();
            ClickOf(emitter).Should().Be(UiInteraction.Click);
        }

        [Test]
        public void SelectorPrefab_PreviousAndNext_EmitStep()
        {
            // Act
            var emitters = LoadPrefab("Shared/UI/Prefabs/Selector.prefab").GetComponentsInChildren<UiInteractionEmitter>(true);

            // Assert
            emitters.Select(emitter => emitter.name).Should().BeEquivalentTo("Previous", "Next");
            emitters.Select(ClickOf).Should().AllBeEquivalentTo(UiInteraction.Step);
        }

        [Test]
        public void SliderPrefab_Slider_HasHoverOnlyEmitter()
        {
            // Act
            var slider = LoadPrefab("Shared/UI/Prefabs/Slider.prefab").GetComponentInChildren<Slider>(true);

            // Assert
            var emitter = slider.GetComponent<UiInteractionEmitter>();
            emitter.Should().NotBeNull();
            ClickOf(emitter).Should().Be(UiInteraction.None);
        }

        [Test]
        public void SettingsScene_ScopeRelay_IsAssigned()
        {
            // Arrange
            OpenSettingsScene();

            // Act
            var scope = Components<MonoBehaviour>().Single(behaviour => behaviour.GetType().Name == "SettingsLifetimeScope");

            // Assert
            new SerializedObject(scope).FindProperty("_uiSounds").objectReferenceValue.Should().BeOfType<UiInteractionRelay>();
        }

        [Test]
        public void SettingsScene_EverySoundSource_HasRelayAncestor()
        {
            // Arrange
            OpenSettingsScene();

            // Act
            var sources = Components<UiInteractionEmitter>().Cast<Component>().Concat(Components<SliderView>()).ToList();

            // Assert
            sources.Should().NotBeEmpty();
            sources.Should().OnlyContain(source => source.GetComponentInParent<UiInteractionRelay>(true) != null);
        }

        [Test]
        public void SettingsScene_BackButton_EmitsBack()
        {
            // Arrange
            OpenSettingsScene();

            // Act
            var back = Components<UiInteractionEmitter>().Single(emitter => emitter.name == "Back");

            // Assert
            ClickOf(back).Should().Be(UiInteraction.Back);
        }

        private static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + path);
        }

        private static UiInteraction ClickOf(UiInteractionEmitter emitter)
        {
            return (UiInteraction)new SerializedObject(emitter).FindProperty("_click").intValue;
        }

        private void OpenSettingsScene()
        {
            _scene = EditorSceneManager.OpenScene(SettingsScenePath, OpenSceneMode.Additive);
        }

        private T[] Components<T>() where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }
    }
}
