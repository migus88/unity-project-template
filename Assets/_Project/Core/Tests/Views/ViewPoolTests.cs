using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
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
    public sealed class ViewPoolTests
    {
        private IViewFactory _factory = null!;
        private Loadable<GameObject> _prefab = null!;
        private Transform _parent = null!;
        private ViewPool<CanvasGroup> _pool = null!;

        private readonly List<GameObject> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _factory = Substitute.For<IViewFactory>();
            _prefab = new Loadable<GameObject>(default);
            _parent = CreateObject("Parent").transform;
            _factory.CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                .Returns(_ => UniTask.FromResult<OneOf<CanvasGroup, NotFound>>(CreateView()));
            _pool = new ViewPool<CanvasGroup>(_factory, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();

            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public async Task RentAsync_EmptyPool_CreatesViewThroughFactory()
        {
            // Act
            var result = await _pool.RentAsync(_parent, CancellationToken.None);

            // Assert
            result.Should().BeCase<CanvasGroup>();
            _ = _factory.Received(1).CreateAsync<CanvasGroup>(_prefab, _parent, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RentAsync_FactoryReturnsNotFound_ReturnsNotFound()
        {
            // Arrange
            _factory.CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<OneOf<CanvasGroup, NotFound>>(new NotFound()));

            // Act
            var result = await _pool.RentAsync(_parent, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task RentAsync_CancelledToken_Throws()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _pool.RentAsync(_parent, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task Return_RentedView_DeactivatesView()
        {
            // Arrange
            var view = await RentAsync();

            // Act
            _pool.Return(view);

            // Assert
            view.gameObject.activeSelf.Should().BeFalse();
        }

        [Test]
        public async Task RentAsync_AfterReturn_ReusesViewUnderNewParent()
        {
            // Arrange
            var view = await RentAsync();
            _pool.Return(view);
            var otherParent = CreateObject("Other Parent").transform;

            // Act
            var reused = await RentAsync(otherParent);

            // Assert
            reused.Should().BeSameAs(view);
            reused.gameObject.activeSelf.Should().BeTrue();
            reused.transform.parent.Should().BeSameAs(otherParent);
            _ = _factory.Received(1).CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RentAsync_IdleViewDestroyedExternally_DestroysItThroughFactoryAndCreatesNewView()
        {
            // Arrange
            var view = await RentAsync();
            _pool.Return(view);
            Object.DestroyImmediate(view.gameObject);

            // Act
            var created = await RentAsync();

            // Assert
            created.Should().NotBeSameAs(view);
            _factory.Received(1).Destroy(view);
            _ = _factory.Received(2).CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void Return_ViewNotRented_Throws()
        {
            // Arrange
            var view = CreateView();

            // Act
            Action act = () => _pool.Return(view);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task Return_Twice_Throws()
        {
            // Arrange
            var view = await RentAsync();
            _pool.Return(view);

            // Act
            Action act = () => _pool.Return(view);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task Return_RentedViewDestroyedExternally_DestroysItThroughFactory()
        {
            // Arrange
            var view = await RentAsync();
            Object.DestroyImmediate(view.gameObject);

            // Act
            _pool.Return(view);

            // Assert
            _factory.Received(1).Destroy(view);
        }

        [Test]
        public async Task Dispose_RentedAndIdleViews_DestroysAllThroughFactory()
        {
            // Arrange
            var rented = await RentAsync();
            var idle = await RentAsync();
            _pool.Return(idle);

            // Act
            _pool.Dispose();

            // Assert
            _factory.Received(1).Destroy(rented);
            _factory.Received(1).Destroy(idle);
        }

        [Test]
        public async Task RentAsync_AfterDispose_Throws()
        {
            // Arrange
            _pool.Dispose();

            // Act
            Func<Task> act = async () => await _pool.RentAsync(_parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Test]
        public async Task Return_AfterDispose_DoesNothing()
        {
            // Arrange
            var view = await RentAsync();
            _pool.Dispose();
            _factory.ClearReceivedCalls();

            // Act
            _pool.Return(view);

            // Assert
            _factory.DidNotReceive().Destroy(Arg.Any<CanvasGroup>());
        }

        [Test]
        public void Dispose_DuringRent_CancelsFactoryToken()
        {
            // Arrange
            var creation = new UniTaskCompletionSource<OneOf<CanvasGroup, NotFound>>();
            var factoryToken = CancellationToken.None;
            _factory.CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Do<CancellationToken>(token => factoryToken = token))
                .Returns(creation.Task);
            _pool.RentAsync(_parent, CancellationToken.None).Forget();

            // Act
            _pool.Dispose();

            // Assert
            factoryToken.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task RentAsync_DisposedWhileFactoryCompletes_DestroysCreatedViewAndThrows()
        {
            // Arrange
            var creation = new UniTaskCompletionSource<OneOf<CanvasGroup, NotFound>>();
            _factory.CreateAsync<CanvasGroup>(_prefab, Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                .Returns(creation.Task);
            var rent = _pool.RentAsync(_parent, CancellationToken.None);
            var view = CreateView();
            _pool.Dispose();

            // Act
            creation.TrySetResult(view);
            Func<Task> act = async () => await rent;

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            _factory.Received(1).Destroy(view);
        }

        private async UniTask<CanvasGroup> RentAsync(Transform? parent = null)
        {
            var result = await _pool.RentAsync(parent ?? _parent, CancellationToken.None);
            return result.Should().BeCase<CanvasGroup>().Which;
        }

        private CanvasGroup CreateView()
        {
            return CreateObject("View").AddComponent<CanvasGroup>();
        }

        private GameObject CreateObject(string name)
        {
            var createdObject = new GameObject(name);
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
