using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shared.UI.Tests
{
    public sealed class UiInteractionEmitterTests
    {
        private UiInteractionTestCanvas _canvas = null!;

        [SetUp]
        public void SetUp()
        {
            _canvas = new UiInteractionTestCanvas();
        }

        [TearDown]
        public void TearDown()
        {
            _canvas.Destroy();
        }

        [Test]
        public void OnPointerEnter_InteractableButton_EmitsHover()
        {
            // Arrange
            var emitter = CreateEmitter<Button>(UiInteraction.Click, out _);

            // Act
            emitter.OnPointerEnter(new PointerEventData(null));

            // Assert
            _canvas.Received.Should().Equal(UiInteraction.Hover);
        }

        [Test]
        public void OnPointerEnter_NonInteractableButton_EmitsNothing()
        {
            // Arrange
            var emitter = CreateEmitter<Button>(UiInteraction.Click, out var button);
            button.interactable = false;

            // Act
            emitter.OnPointerEnter(new PointerEventData(null));

            // Assert
            _canvas.Received.Should().BeEmpty();
        }

        [TestCase(UiInteraction.Click)]
        [TestCase(UiInteraction.Back)]
        [TestCase(UiInteraction.Step)]
        public void OnClick_Button_EmitsConfiguredClick(UiInteraction click)
        {
            // Arrange
            CreateEmitter<Button>(click, out var button);

            // Act
            button.onClick.Invoke();

            // Assert
            _canvas.Received.Should().Equal(click);
        }

        [Test]
        public void OnPointerEnter_Slider_EmitsHoverOnly()
        {
            // Arrange
            var emitter = CreateEmitter<Slider>(UiInteraction.None, out var slider);

            // Act
            emitter.OnPointerEnter(new PointerEventData(null));
            slider.value = 0.5f;

            // Assert
            _canvas.Received.Should().Equal(UiInteraction.Hover);
        }

        private UiInteractionEmitter CreateEmitter<TSelectable>(UiInteraction click, out TSelectable selectable)
            where TSelectable : Selectable
        {
            var target = _canvas.CreateChild("Selectable");
            selectable = target.AddComponent<TSelectable>();
            var emitter = target.AddComponent<UiInteractionEmitter>();
            var serialized = new SerializedObject(emitter);
            serialized.FindProperty("_click").intValue = (int)click;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            target.SetActive(true);
            UiInteractionTestCanvas.Awaken(emitter);
            return emitter;
        }
    }
}
