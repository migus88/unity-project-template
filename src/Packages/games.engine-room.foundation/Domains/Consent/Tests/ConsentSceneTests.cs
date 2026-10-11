using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Consent.Prompt;
using Core.Audio;
using NUnit.Framework;
using Shared.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Consent.Tests
{
    public sealed class ConsentSceneTests
    {
        private const string ScenePath = "Packages/games.engine-room.foundation/Domains/Consent/Scenes/Consent.unity";
        private const float Tolerance = 0.5f;

        private Scene _scene;
        private ConsentLifetimeScope _scope = null!;
        private ConsentPromptView _view = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SceneManager.GetSceneByPath(ScenePath).isLoaded.Should().BeFalse($"{ScenePath} must be closed before its tests run");
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _scope = FindAll<ConsentLifetimeScope>().Single();
            _view = FindAll<ConsentPromptView>().Single();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void SerializedReferences_ScopeAndView_AreAllAssigned()
        {
            // Act
            var missing = FindMissingReferences(_scope).Concat(FindMissingReferences(_view)).ToList();

            // Assert
            missing.Should().BeEmpty();
        }

        [Test]
        public void Scope_AutoRunsWithoutParent()
        {
            // Arrange
            using var serializedScope = new SerializedObject(_scope);

            // Act
            var isAutoRun = serializedScope.FindProperty("autoRun").boolValue;
            var parentTypeName = serializedScope.FindProperty("parentReference.TypeName").stringValue;

            // Assert
            isAutoRun.Should().BeTrue();
            parentTypeName.Should().BeNullOrEmpty();
        }

        [Test]
        public void Buttons_EmitThroughRelay_NoAndCloseSoundAsBack()
        {
            // Arrange
            var relay = _view.GetComponentInParent<UiInteractionRelay>(true);

            // Act
            var clicks = new Dictionary<string, UiInteraction>();
            foreach (var field in new[] { "_yes", "_no", "_close", "_policy" })
            {
                var button = ButtonOf(field);
                button.GetComponentInParent<UiInteractionRelay>(true).Should().BeSameAs(relay);
                using var serializedEmitter = new SerializedObject(button.GetComponent<UiInteractionEmitter>());
                clicks[field] = (UiInteraction)serializedEmitter.FindProperty("_click").intValue;
            }

            // Assert
            relay.Should().NotBeNull();
            clicks.Should().Equal(new Dictionary<string, UiInteraction>
            {
                ["_yes"] = UiInteraction.Click,
                ["_no"] = UiInteraction.Back,
                ["_close"] = UiInteraction.Back,
                ["_policy"] = UiInteraction.Click,
            });
        }

        [Test]
        public void Layout_BackdropBlocksCanvasAndCloseSitsInPanelClearOfTitle()
        {
            // Arrange
            var canvas = (RectTransform)_view.transform;
            var backdrop = (RectTransform)canvas.Find("Backdrop");
            var panel = (RectTransform)canvas.Find("Panel");
            var title = (RectTransform)panel.Find("Title");
            var close = (RectTransform)ButtonOf("_close").transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            // Act
            var canvasRect = RectIn(canvas, canvas);
            var backdropRect = RectIn(canvas, backdrop);
            var panelRect = RectIn(canvas, panel);
            var closeRect = RectIn(canvas, close);
            var titleRect = RectIn(canvas, title);

            // Assert
            backdrop.GetComponent<Image>().raycastTarget.Should().BeTrue();
            backdropRect.min.x.Should().BeApproximately(canvasRect.min.x, Tolerance);
            backdropRect.min.y.Should().BeApproximately(canvasRect.min.y, Tolerance);
            backdropRect.max.x.Should().BeApproximately(canvasRect.max.x, Tolerance);
            backdropRect.max.y.Should().BeApproximately(canvasRect.max.y, Tolerance);
            panelRect.yMin.Should().BeGreaterThanOrEqualTo(canvasRect.yMin);
            panelRect.yMax.Should().BeLessThanOrEqualTo(canvasRect.yMax);
            closeRect.xMax.Should().BeLessThanOrEqualTo(panelRect.xMax + Tolerance);
            closeRect.yMax.Should().BeLessThanOrEqualTo(panelRect.yMax + Tolerance);
            closeRect.Overlaps(titleRect).Should().BeFalse();
        }

        private Button ButtonOf(string field)
        {
            using var serializedView = new SerializedObject(_view);
            return (Button)serializedView.FindProperty(field).objectReferenceValue;
        }

        private static Rect RectIn(RectTransform space, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = space.InverseTransformPoint(corners[0]);
            var max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static IEnumerable<string> FindMissingReferences(Object target)
        {
            using var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null)
                {
                    yield return $"{target.GetType().Name}.{property.propertyPath}";
                }
            }
        }

        private IEnumerable<T> FindAll<T>() where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }
    }
}
