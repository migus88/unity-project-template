using AwesomeAssertions;
using Bootstrap.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Bootstrap.Tests
{
    public sealed class BootstrapSceneTests
    {
        private const string GameBootPath = "Assets/_Project/Game/Scenes/Boot.unity";
        private const string LevelPath = "Assets/_Project/Game/Scenes/Level.unity";

        [Test]
        public void CountGameObjectsAt_BuildSettingsBootScene_ReturnsZero()
        {
            // Act
            var count = BootstrapScene.CountGameObjectsAt(BootstrapScene.FindBootPath());

            // Assert
            count.Should().Be(0, "the boot scene stays empty; app-lifetime objects belong in the root prefab");
        }

        [Test]
        public void CountGameObjectsAt_PackageScene_ReturnsZero()
        {
            // Act
            var count = BootstrapScene.CountGameObjectsAt(BootstrapScene.PackagePath);

            // Assert
            count.Should().Be(0);
        }

        [Test]
        public void CountGameObjects_GameObjectAndPrefabInstance_CountsBoth()
        {
            // Arrange
            const string sceneText = "%YAML 1.1\n--- !u!1 &100\nGameObject:\n--- !u!4 &101\nTransform:\n--- !u!1001 &200\nPrefabInstance:\n";

            // Act
            var count = BootstrapScene.CountGameObjects(sceneText);

            // Assert
            count.Should().Be(2);
        }

        [Test]
        public void PlaceFirst_PackageSceneListed_ReplacesIt()
        {
            // Arrange
            var scenes = new[] { new EditorBuildSettingsScene(BootstrapScene.PackagePath, true) };

            // Act
            var result = BootstrapScene.PlaceFirst(scenes, GameBootPath);

            // Assert
            result.Should().ContainSingle();
            result[0].path.Should().Be(GameBootPath);
            result[0].enabled.Should().BeTrue();
        }

        [Test]
        public void PlaceFirst_PathListedDisabledAfterOthers_MovesItFirstEnabledAndKeepsOthers()
        {
            // Arrange
            var scenes = new[]
            {
                new EditorBuildSettingsScene(LevelPath, true),
                new EditorBuildSettingsScene(GameBootPath, false),
            };

            // Act
            var result = BootstrapScene.PlaceFirst(scenes, GameBootPath);

            // Assert
            result.Should().HaveCount(2);
            result[0].path.Should().Be(GameBootPath);
            result[0].enabled.Should().BeTrue();
            result[1].path.Should().Be(LevelPath);
        }
    }
}
