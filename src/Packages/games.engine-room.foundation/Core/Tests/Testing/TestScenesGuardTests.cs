using AwesomeAssertions;
using NUnit.Framework;
using TestUtils;
using UnityEngine;

namespace Core.Tests.Testing
{
    public sealed class TestScenesGuardTests
    {
        private const string PrefabPath = "Packages/games.engine-room.foundation/Shared/UI/Prefabs/Button.prefab";

        [Test]
        public void LoadPrefab_SecondTimeInOneFixture_FailsTheTest()
        {
            // Arrange
            var first = TestScenes.LoadPrefab(PrefabPath);
            GameObject? second = null;

            try
            {
                // Act
                var failure = Assert.Throws<AssertionException>(() => second = TestScenes.LoadPrefab(PrefabPath));

                // Assert
                failure.Message.Should().Contain(PrefabPath).And.Contain(nameof(TestScenesGuardTests));
            }
            finally
            {
                TestScenes.UnloadPrefab(second);
                TestScenes.UnloadPrefab(first);
            }
        }
    }
}
