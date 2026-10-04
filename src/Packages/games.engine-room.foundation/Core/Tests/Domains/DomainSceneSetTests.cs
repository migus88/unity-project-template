using System;
using System.Collections.Generic;
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
using R3;
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

        [TearDown]
        public void TearDown()
        {
            _sceneSet.Dispose();
        }

        [Test]
        public async Task LoadAsync_SceneFound_EmitsSceneLoaded()
        {
            // Arrange
            ReturnFromLoader(default(Scene));
            var loadedScenes = new List<Scene>();
            using var subscription = _sceneSet.SceneLoaded.Subscribe(loadedScenes.Add);

            // Act
            await _sceneSet.LoadAsync(default, CancellationToken.None);

            // Assert
            loadedScenes.Should().ContainSingle();
        }

        [Test]
        public async Task LoadAsync_SceneNotFound_DoesNotEmitSceneLoaded()
        {
            // Arrange
            ReturnFromLoader(new NotFound());
            var loadedScenes = new List<Scene>();
            using var subscription = _sceneSet.SceneLoaded.Subscribe(loadedScenes.Add);

            // Act
            await _sceneSet.LoadAsync(default, CancellationToken.None);

            // Assert
            loadedScenes.Should().BeEmpty();
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
        public async Task UnloadAllAsync_LoadInFlight_WaitsForLoadThenUnloadsItsScene()
        {
            // Arrange
            var load = new UniTaskCompletionSource<OneOf<Scene, NotFound>>();
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>()).Returns(load.Task);
            var loading = _sceneSet.LoadAsync(default, CancellationToken.None);

            // Act
            var unloading = _sceneSet.UnloadAllAsync(CancellationToken.None);
            var wasPendingBeforeLoad = unloading.Status == UniTaskStatus.Pending;
            load.TrySetResult(default(Scene));
            await loading;
            await unloading;

            // Assert
            wasPendingBeforeLoad.Should().BeTrue();
            _ = _sceneLoader.ReceivedWithAnyArgs(1).UnloadAsync(default, default);
        }

        [Test]
        public async Task UnloadAllAsync_LoadInFlightCancelled_UnloadsNothing()
        {
            // Arrange
            var load = new UniTaskCompletionSource<OneOf<Scene, NotFound>>();
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>()).Returns(load.Task);
            var loading = _sceneSet.LoadAsync(default, CancellationToken.None);

            // Act
            var unloading = _sceneSet.UnloadAllAsync(CancellationToken.None);
            load.TrySetCanceled();
            var (wasLoadCancelled, _) = await loading.SuppressCancellationThrow();
            await unloading;

            // Assert
            wasLoadCancelled.Should().BeTrue();
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().UnloadAsync(default, default);
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
