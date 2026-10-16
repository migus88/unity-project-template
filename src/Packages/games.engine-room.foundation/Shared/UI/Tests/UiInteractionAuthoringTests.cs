using System.Linq;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI.Tests
{
    public sealed class UiInteractionAuthoringTests
    {
        private const string Root = "Packages/games.engine-room.foundation/";

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

        private static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + path);
        }

        internal static UiInteraction ClickOf(UiInteractionEmitter emitter)
        {
            return (UiInteraction)new SerializedObject(emitter).FindProperty("_click").intValue;
        }
    }
}
