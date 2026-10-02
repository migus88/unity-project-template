using AwesomeAssertions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using Object = UnityEngine.Object;

namespace Bootstrap.Tests
{
    public sealed class RootPrefabTests
    {
        private const string RootPrefabPath = "Packages/games.engine-room.foundation/Bootstrap/Prefabs/RootLifetimeScope.prefab";

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
