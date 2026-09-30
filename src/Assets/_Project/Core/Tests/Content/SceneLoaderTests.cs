using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Results;
using NUnit.Framework;
using TestUtils;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Tests.Content
{
    public sealed class SceneLoaderTests
    {
        private SceneLoader _loader = null!;

        [SetUp]
        public void SetUp()
        {
            _loader = new SceneLoader();
        }

        [Test]
        public async Task LoadAdditiveAsync_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var id = default(LoadableSceneId);

            // Act
            var result = await _loader.LoadAdditiveAsync(id, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task LoadAdditiveAsync_CancelledToken_Throws()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _loader.LoadAdditiveAsync(default, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task UnloadAsync_SceneNotLoaded_CompletesWithoutThrowing()
        {
            // Arrange
            var scene = default(Scene);

            // Act
            Func<Task> act = async () => await _loader.UnloadAsync(scene, CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
