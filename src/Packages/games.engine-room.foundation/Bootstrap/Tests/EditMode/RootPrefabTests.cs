using AwesomeAssertions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Bootstrap.Tests
{
    public sealed class RootPrefabTests
    {
        private const string RootPrefabPath = "Packages/games.engine-room.foundation/Bootstrap/Prefabs/RootLifetimeScope.prefab";
        private const int LoadingCanvasSortingOrder = 1000;

        private GameModule? _module;

        [TearDown]
        public void TearDown()
        {
            if (_module != null)
            {
                Object.DestroyImmediate(_module);
            }
        }

        [Test]
        public void EventSystem_Authored_DoesNotSendNavigationEvents()
        {
            // Arrange
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);

            // Act
            var eventSystem = root.GetComponentInChildren<EventSystem>(true);

            // Assert
            eventSystem.Should().NotBeNull();
            eventSystem.sendNavigationEvents.Should().BeFalse();
        }

        [Test]
        public void RootLifetimeScope_Authored_ReferencesTheRootEventSystem()
        {
            // Arrange
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            var scope = new SerializedObject(root.GetComponent<RootLifetimeScope>());

            // Act
            var reference = scope.FindProperty("_eventSystem").objectReferenceValue;

            // Assert
            reference.Should().BeSameAs(root.GetComponentInChildren<EventSystem>(true));
        }

        [Test]
        public void BootCover_Authored_IsEnabledOverlayCanvasAboveLoading()
        {
            // Arrange
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);

            // Act
            var canvas = root.GetComponentInChildren<BootCoverView>(true).GetComponent<Canvas>();

            // Assert
            canvas.enabled.Should().BeTrue();
            canvas.gameObject.activeSelf.Should().BeTrue();
            canvas.renderMode.Should().Be(RenderMode.ScreenSpaceOverlay);
            canvas.sortingOrder.Should().BeGreaterThan(LoadingCanvasSortingOrder);
        }

        [Test]
        public void BootCover_Authored_HasOpaqueFullScreenImage()
        {
            // Arrange
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);

            // Act
            var image = root.GetComponentInChildren<BootCoverView>(true).GetComponentInChildren<Image>(true);

            // Assert
            image.enabled.Should().BeTrue();
            image.color.a.Should().Be(1f);
            image.rectTransform.anchorMin.Should().Be(Vector2.zero);
            image.rectTransform.anchorMax.Should().Be(Vector2.one);
            image.rectTransform.offsetMin.Should().Be(Vector2.zero);
            image.rectTransform.offsetMax.Should().Be(Vector2.zero);
        }

        [Test]
        public void RootLifetimeScope_Authored_ReferencesTheBootCover()
        {
            // Arrange
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            var scope = new SerializedObject(root.GetComponent<RootLifetimeScope>());

            // Act
            var reference = scope.FindProperty("_bootCover").objectReferenceValue;

            // Assert
            reference.Should().NotBeNull();
            reference.Should().BeSameAs(root.GetComponentInChildren<BootCoverView>(true));
        }

        [Test]
        public void IsUiNavigationEnabled_NewModule_IsFalse()
        {
            // Arrange
            _module = ScriptableObject.CreateInstance<EmptyGameModule>();

            // Act
            var isEnabled = _module.IsUiNavigationEnabled;

            // Assert
            isEnabled.Should().BeFalse();
        }

        private sealed class EmptyGameModule : GameModule
        {
            public override void Install(IContainerBuilder builder)
            {
            }
        }
    }
}
