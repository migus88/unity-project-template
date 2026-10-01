using System.Threading;
using AwesomeAssertions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Loading.Tests
{
    public sealed class LoadingScreenViewTests
    {
        private GameObject _viewObject = null!;
        private Canvas _canvas = null!;
        private CanvasGroup _canvasGroup = null!;
        private LoadingScreenView _view = null!;

        [SetUp]
        public void SetUp()
        {
            _viewObject = new GameObject("Loading Screen View");
            _canvas = _viewObject.AddComponent<Canvas>();
            _canvasGroup = _viewObject.AddComponent<CanvasGroup>();
            var spinner = _viewObject.AddComponent<Animator>();
            _view = _viewObject.AddComponent<LoadingScreenView>();

            using var serializedView = new SerializedObject(_view);
            serializedView.FindProperty("_canvas").objectReferenceValue = _canvas;
            serializedView.FindProperty("_canvasGroup").objectReferenceValue = _canvasGroup;
            serializedView.FindProperty("_spinner").objectReferenceValue = spinner;
            serializedView.FindProperty("_fadeSeconds").floatValue = 0f;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_viewObject);
        }

        [TestCase(true, 1f)]
        [TestCase(false, 0f)]
        public void FadeAsync_ZeroFadeSeconds_AppliesTargetAtOnce(bool isVisible, float expectedAlpha)
        {
            // Arrange
            _view.SetVisible(!isVisible);

            // Act
            var fade = _view.FadeAsync(isVisible, CancellationToken.None);

            // Assert
            fade.Status.Should().Be(UniTaskStatus.Succeeded);
            _canvasGroup.alpha.Should().Be(expectedAlpha);
            _canvas.enabled.Should().Be(isVisible);
            _canvasGroup.blocksRaycasts.Should().Be(isVisible);
        }
    }
}
