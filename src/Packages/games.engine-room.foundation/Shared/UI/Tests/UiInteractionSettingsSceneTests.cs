using System.Linq;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using TestUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shared.UI.Tests
{
    public sealed class UiInteractionSettingsSceneTests
    {
        private const string SettingsScenePath = "Packages/games.engine-room.foundation/Domains/Settings/Scenes/Settings.unity";

        private Scene _scene;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _scene = TestScenes.Open(SettingsScenePath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestScenes.Close(_scene);
        }

        [Test]
        public void SettingsScene_ScopeRelay_IsAssigned()
        {
            // Act
            var scope = Components<MonoBehaviour>().Single(behaviour => behaviour.GetType().Name == "SettingsLifetimeScope");

            // Assert
            new SerializedObject(scope).FindProperty("_uiSounds").objectReferenceValue.Should().BeOfType<UiInteractionRelay>();
        }

        [Test]
        public void SettingsScene_EverySoundSource_HasRelayAncestor()
        {
            // Act
            var sources = Components<UiInteractionEmitter>().Cast<Component>().Concat(Components<SliderView>()).ToList();

            // Assert
            sources.Should().NotBeEmpty();
            sources.Should().OnlyContain(source => source.GetComponentInParent<UiInteractionRelay>(true) != null);
        }

        [Test]
        public void SettingsScene_BackButton_EmitsBack()
        {
            // Act
            var back = Components<UiInteractionEmitter>().Single(emitter => emitter.name == "Back");

            // Assert
            UiInteractionAuthoringTests.ClickOf(back).Should().Be(UiInteraction.Back);
        }

        private T[] Components<T>() where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }
    }
}
