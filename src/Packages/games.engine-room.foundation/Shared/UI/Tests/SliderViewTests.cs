using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace Shared.UI.Tests
{
    public sealed class SliderViewTests
    {
        private UiInteractionTestCanvas _canvas = null!;
        private Slider _slider = null!;
        private TextMeshProUGUI _label = null!;
        private SliderView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _canvas = new UiInteractionTestCanvas();
            var root = _canvas.CreateChild("SliderView");
            _slider = root.AddComponent<Slider>();
            _label = _canvas.CreateChild("Label").AddComponent<TextMeshProUGUI>();
            _view = root.AddComponent<SliderView>();
            var serialized = new SerializedObject(_view);
            serialized.FindProperty("_slider").objectReferenceValue = _slider;
            serialized.FindProperty("_valueLabel").objectReferenceValue = _label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            UiInteractionTestCanvas.Awaken(_view);
        }

        [TearDown]
        public void TearDown()
        {
            _canvas.Destroy();
        }

        [Test]
        public void SetValue_FromPresenter_EmitsNothing()
        {
            // Act
            _view.SetValue(0.4f);

            // Assert
            _canvas.Received.Should().BeEmpty();
            _label.text.Should().Be("40%");
        }

        [Test]
        public void SliderValue_ChangedByUser_EmitsTickAndShowsValue()
        {
            // Act
            _slider.value = 0.7f;

            // Assert
            _canvas.Received.Should().Equal(UiInteraction.Tick);
            _label.text.Should().Be("70%");
        }
    }
}
