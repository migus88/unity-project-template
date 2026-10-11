using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using Settings.Privacy;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Settings.Tests
{
    public sealed class SettingsSceneTests
    {
        private const string ScenePath = "Packages/games.engine-room.foundation/Domains/Settings/Scenes/Settings.unity";
        private const float ReferenceHeight = 1080f;
        private const float Tolerance = 0.5f;

        private Scene _scene;
        private SettingsLifetimeScope _scope = null!;
        private PrivacyRowView _privacy = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SceneManager.GetSceneByPath(ScenePath).isLoaded.Should().BeFalse($"{ScenePath} must be closed before its tests run");
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _scope = FindAll<SettingsLifetimeScope>().Single();
            _privacy = FindAll<PrivacyRowView>().Single();
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
        public void SerializedReferences_ScopeAndPrivacyRow_AreAllAssigned()
        {
            // Act
            var missing = FindMissingReferences(_scope).Concat(FindMissingReferences(_privacy)).ToList();

            // Assert
            missing.Should().BeEmpty();
        }

        [Test]
        public void InstallId_IsReadOnly()
        {
            // Arrange
            using var serializedView = new SerializedObject(_privacy);

            // Act
            var installId = (TMP_InputField)serializedView.FindProperty("_installId").objectReferenceValue;

            // Assert
            installId.readOnly.Should().BeTrue();
        }

        [Test]
        public void PrivacyRow_SitsInsidePanelBelowLanguageAndPanelFitsReferenceHeight()
        {
            // Arrange
            var panel = (RectTransform)_privacy.transform.parent;
            var language = (RectTransform)panel.Find("Language");
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            // Act
            var panelRect = panel.rect;
            var privacyRect = RectIn(panel, (RectTransform)_privacy.transform);
            var languageRect = RectIn(panel, language);

            // Assert
            panelRect.height.Should().BeLessThanOrEqualTo(ReferenceHeight);
            privacyRect.xMin.Should().BeGreaterThanOrEqualTo(panelRect.xMin - Tolerance);
            privacyRect.xMax.Should().BeLessThanOrEqualTo(panelRect.xMax + Tolerance);
            privacyRect.yMin.Should().BeGreaterThanOrEqualTo(panelRect.yMin - Tolerance);
            privacyRect.yMax.Should().BeLessThanOrEqualTo(languageRect.yMin + Tolerance);
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
