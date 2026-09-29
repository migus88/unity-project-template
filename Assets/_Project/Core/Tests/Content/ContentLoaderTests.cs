using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Results;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TestUtils;
using Unity.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Tests.Content
{
    public sealed class ContentLoaderTests
    {
        private const string AssetPath = "Assets/_Project/Core/Input/GameInput.inputactions";

        private ContentLoader _loader = null!;

        [SetUp]
        public void SetUp()
        {
            _loader = new ContentLoader();
        }

        [Test]
        public async Task LoadAsync_InvalidLoadable_ReturnsNotFound()
        {
            // Arrange
            var loadable = new Loadable<GameObject>(default);

            // Act
            var result = await _loader.LoadAsync(loadable, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task LoadAsync_CancelledToken_Throws()
        {
            // Arrange
            var loadable = new Loadable<GameObject>(default);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _loader.LoadAsync(loadable, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public void Release_NeverLoaded_LeavesStatusNone()
        {
            // Arrange
            var loadable = new Loadable<GameObject>(default);

            // Act
            _loader.Release(loadable);

            // Assert
            loadable.Status.Should().Be(LoadableStatus.None);
        }

        [Test]
        public async Task Release_AfterTwoLoads_KeepsAssetLoaded()
        {
            // Arrange
            var loadable = CreateLoadable();
            await _loader.LoadAsync(loadable, CancellationToken.None);
            await _loader.LoadAsync(loadable, CancellationToken.None);

            // Act
            _loader.Release(loadable);

            // Assert
            loadable.Status.Should().Be(LoadableStatus.Loaded);
            _loader.Release(loadable);
        }

        [Test]
        public async Task Release_OncePerLoad_UnloadsAsset()
        {
            // Arrange
            var loadable = CreateLoadable();
            await _loader.LoadAsync(loadable, CancellationToken.None);
            await _loader.LoadAsync(loadable, CancellationToken.None);
            _loader.Release(loadable);

            // Act
            _loader.Release(loadable);

            // Assert
            loadable.Status.Should().Be(LoadableStatus.None);
        }

        [Test]
        public async Task LoadAsync_OneOfTwoOverlappingLoadsCancelled_OtherKeepsAssetLoaded()
        {
            // Arrange
            var loadable = CreateLoadable();
            using var cts = new CancellationTokenSource();
            var cancelledLoad = _loader.LoadAsync(loadable, cts.Token).AsTask();
            var otherLoad = _loader.LoadAsync(loadable, CancellationToken.None).AsTask();

            // Act
            cts.Cancel();
            var otherResult = await otherLoad;

            // Assert
            await cancelledLoad.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            otherResult.Should().BeCase<InputActionAsset>();
            loadable.Status.Should().Be(LoadableStatus.Loaded);
            _loader.Release(loadable);
            loadable.Status.Should().Be(LoadableStatus.None);
        }

        private static Loadable<InputActionAsset> CreateLoadable()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            var id = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
            return new Loadable<InputActionAsset>(in id);
        }
    }
}
