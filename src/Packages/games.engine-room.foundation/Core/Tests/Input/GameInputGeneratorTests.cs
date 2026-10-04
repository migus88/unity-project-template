using System.IO;
using AwesomeAssertions;
using Core.Editor.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Core.Tests.Input
{
    public sealed class GameInputGeneratorTests
    {
        [Test]
        public void BuildSource_GameInputAsset_MatchesCommittedWrapper()
        {
            // Arrange
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GameInputGenerator.AssetPath);
            var committed = File.ReadAllText(GameInputGenerator.ToPhysicalPath(GameInputGenerator.OutputPath));

            // Act
            var source = GameInputGenerator.BuildSource(asset);

            // Assert
            source.Should().Be(committed);
        }
    }
}
