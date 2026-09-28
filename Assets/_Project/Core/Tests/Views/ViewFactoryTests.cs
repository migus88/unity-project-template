using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Results;
using Core.Views;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using TestUtils;
using Unity.Loading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Views
{
    public sealed class ViewFactoryTests
    {
        private IContentLoader _contentLoader = null!;
        private Loadable<GameObject> _prefab = null!;
        private GameObject _parent = null!;
        private ViewFactory _factory = null!;

        [SetUp]
        public void SetUp()
        {
            _contentLoader = Substitute.For<IContentLoader>();
            _prefab = new Loadable<GameObject>(default);
            _parent = new GameObject("Parent");
            _factory = new ViewFactory(_contentLoader);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
        }

        [Test]
        public async Task CreateAsync_PrefabNotFound_ReturnsNotFoundAndReleasesPrefab()
        {
            // Arrange
            _contentLoader.LoadAsync(_prefab, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<OneOf<GameObject, NotFound>>(new NotFound()));

            // Act
            var result = await _factory.CreateAsync<CanvasGroup>(_prefab, _parent.transform, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
            _contentLoader.Received(1).Release(_prefab);
        }

        [Test]
        public async Task CreateAsync_CancelledToken_ThrowsWithoutLoading()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _factory.CreateAsync<CanvasGroup>(_prefab, _parent.transform, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _ = _contentLoader.DidNotReceiveWithAnyArgs().LoadAsync(_prefab, default);
        }

        [Test]
        public async Task CreateAsync_LoadCancelled_ReleasesPrefab()
        {
            // Arrange
            _contentLoader.LoadAsync(_prefab, Arg.Any<CancellationToken>())
                .Returns(UniTask.FromException<OneOf<GameObject, NotFound>>(new OperationCanceledException()));

            // Act
            Func<Task> act = async () => await _factory.CreateAsync<CanvasGroup>(_prefab, _parent.transform, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _contentLoader.Received(1).Release(_prefab);
        }

        [Test]
        public void Destroy_ViewNotCreatedByFactory_Throws()
        {
            // Arrange
            var view = _parent.AddComponent<CanvasGroup>();

            // Act
            Action act = () => _factory.Destroy(view);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
