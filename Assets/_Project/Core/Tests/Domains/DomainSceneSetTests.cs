using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Domains;
using Core.Results;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Tests.Domains
{
    public sealed class DomainSceneSetTests
    {
        private ISceneLoader _sceneLoader = null!;
        private DomainSceneSet _sceneSet = null!;

        [SetUp]
        public void SetUp()
        {
            _sceneLoader = Substitute.For<ISceneLoader>();
            _sceneSet = new DomainSceneSet(_sceneLoader);
        }

        [Test]
        public async Task LoadAsync_SceneFound_ReturnsScene()
        {
            // Arrange
            ReturnFromLoader(default(Scene));

            // Act
            var result = await _sceneSet.LoadAsync(default, CancellationToken.None);

            // Assert
            result.Should().BeCase<Scene>();
        }

        [Test]
        public async Task UnloadAllAsync_AfterTwoLoads_UnloadsBothScenes()
        {
            // Arrange
            ReturnFromLoader(default(Scene));
            await _sceneSet.LoadAsync(default, CancellationToken.None);
            await _sceneSet.LoadAsync(default, CancellationToken.None);

            // Act
            await _sceneSet.UnloadAllAsync(CancellationToken.None);

            // Assert
            _ = _sceneLoader.ReceivedWithAnyArgs(2).UnloadAsync(default, default);
        }

        [Test]
        public async Task UnloadAllAsync_SceneNotFound_UnloadsNothing()
        {
            // Arrange
            ReturnFromLoader(new NotFound());
            var result = await _sceneSet.LoadAsync(default, CancellationToken.None);

            // Act
            await _sceneSet.UnloadAllAsync(CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().UnloadAsync(default, default);
        }

        [Test]
        public async Task UnloadAllAsync_SceneAlreadyUnloaded_DoesNotUnloadItAgain()
        {
            // Arrange
            ReturnFromLoader(default(Scene));
            var loadResult = await _sceneSet.LoadAsync(default, CancellationToken.None);
            var scene = loadResult.Should().BeCase<Scene>().Which;
            await _sceneSet.UnloadAsync(scene, CancellationToken.None);

            // Act
            await _sceneSet.UnloadAllAsync(CancellationToken.None);

            // Assert
            _ = _sceneLoader.ReceivedWithAnyArgs(1).UnloadAsync(default, default);
        }

        [Test]
        public async Task UnloadAsync_SceneNotLoadedBySet_Throws()
        {
            // Arrange
            var scene = default(Scene);

            // Act
            Func<Task> act = async () => await _sceneSet.UnloadAsync(scene, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().UnloadAsync(default, default);
        }

        private void ReturnFromLoader(OneOf<Scene, NotFound> result)
        {
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(result));
        }
    }
}
