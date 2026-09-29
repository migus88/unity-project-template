using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Input;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace Loading.Tests
{
    public sealed class LoadingScreenTests
    {
        private ILockService<InputLockTag> _inputLocks = null!;
        private ILock<InputLockTag> _inputLock = null!;
        private FakeLoadingScreenView _view = null!;
        private LoadingScreen _screen = null!;

        [SetUp]
        public void SetUp()
        {
            _inputLocks = Substitute.For<ILockService<InputLockTag>>();
            _inputLock = Substitute.For<ILock<InputLockTag>>();
            _inputLocks.LockAll().Returns(_inputLock);
            _view = new FakeLoadingScreenView();
            _screen = new LoadingScreen(_inputLocks);
        }

        [TearDown]
        public void TearDown()
        {
            _screen.Dispose();
        }

        [Test]
        public async Task ShowAsync_NoViewAttached_LocksInputAndCompletes()
        {
            // Act
            await _screen.ShowAsync(CancellationToken.None);

            // Assert
            _inputLocks.Received(1).LockAll();
            _inputLock.DidNotReceive().Dispose();
        }

        [Test]
        public async Task ShowAsync_ViewAttached_FadesViewIn()
        {
            // Arrange
            _screen.Attach(_view);

            // Act
            await _screen.ShowAsync(CancellationToken.None);

            // Assert
            _view.Fades.Should().Equal(true);
        }

        [Test]
        public void ShowAsync_WhileFadingIn_WaitsForTheSameFade()
        {
            // Arrange
            var fadeIn = new UniTaskCompletionSource();
            _view.FadeIn = fadeIn.Task;
            _screen.Attach(_view);
            var first = _screen.ShowAsync(CancellationToken.None);

            // Act
            var second = _screen.ShowAsync(CancellationToken.None);
            var isSecondCompletedBeforeFade = second.Status.IsCompleted();
            fadeIn.TrySetResult();

            // Assert
            isSecondCompletedBeforeFade.Should().BeFalse();
            first.Status.Should().Be(UniTaskStatus.Succeeded);
            second.Status.Should().Be(UniTaskStatus.Succeeded);
            _view.Fades.Should().Equal(true);
            _inputLocks.Received(1).LockAll();
        }

        [Test]
        public async Task HideAsync_AfterShow_FadesViewOutAndReleasesInputLock()
        {
            // Arrange
            _screen.Attach(_view);
            await _screen.ShowAsync(CancellationToken.None);

            // Act
            await _screen.HideAsync(CancellationToken.None);

            // Assert
            _view.Fades.Should().Equal(true, false);
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public void HideAsync_WhileFadingOut_KeepsInputLockedUntilFadeEnds()
        {
            // Arrange
            var fadeOut = new UniTaskCompletionSource();
            _view.FadeOut = fadeOut.Task;
            _screen.Attach(_view);
            _screen.ShowAsync(CancellationToken.None).Forget();

            // Act
            var hide = _screen.HideAsync(CancellationToken.None);

            // Assert
            hide.Status.IsCompleted().Should().BeFalse();
            _inputLock.DidNotReceive().Dispose();
            fadeOut.TrySetResult();
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public void ShowAsync_DuringFadeOut_KeepsInputLockedAfterFadeOutEnds()
        {
            // Arrange
            var fadeOut = new UniTaskCompletionSource();
            _view.FadeOut = fadeOut.Task;
            _screen.Attach(_view);
            _screen.ShowAsync(CancellationToken.None).Forget();
            _screen.HideAsync(CancellationToken.None).Forget();

            // Act
            _screen.ShowAsync(CancellationToken.None).Forget();
            fadeOut.TrySetResult();

            // Assert
            _view.Fades.Should().Equal(true, false, true);
            _inputLock.DidNotReceive().Dispose();
            _inputLocks.Received(1).LockAll();
        }

        [Test]
        public async Task HideAsync_WhileHidden_DoesNothing()
        {
            // Arrange
            _screen.Attach(_view);

            // Act
            await _screen.HideAsync(CancellationToken.None);

            // Assert
            _view.Fades.Should().BeEmpty();
            _inputLocks.DidNotReceive().LockAll();
        }

        [Test]
        public async Task Attach_WhileShown_ShowsViewImmediately()
        {
            // Arrange
            await _screen.ShowAsync(CancellationToken.None);

            // Act
            _screen.Attach(_view);

            // Assert
            _view.VisibilitySets.Should().Equal(true);
            _view.Fades.Should().BeEmpty();
        }

        [Test]
        public void Attach_WhileHidden_HidesViewImmediately()
        {
            // Act
            _screen.Attach(_view);

            // Assert
            _view.VisibilitySets.Should().Equal(false);
        }

        [Test]
        public async Task ShowAsync_AfterDetach_DoesNotTouchView()
        {
            // Arrange
            var attachment = _screen.Attach(_view);
            attachment.Dispose();

            // Act
            await _screen.ShowAsync(CancellationToken.None);

            // Assert
            _view.Fades.Should().BeEmpty();
            _inputLocks.Received(1).LockAll();
        }

        [Test]
        public async Task HideAsync_ViewFadeCancelled_CompletesAndReleasesInputLock()
        {
            // Arrange
            _view.FadeOut = UniTask.FromCanceled();
            _screen.Attach(_view);
            await _screen.ShowAsync(CancellationToken.None);

            // Act
            Func<Task> act = () => _screen.HideAsync(CancellationToken.None).AsTask();

            // Assert
            await act.Should().NotThrowAsync();
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public async Task Dispose_WhileShown_ReleasesInputLock()
        {
            // Arrange
            var screen = new LoadingScreen(_inputLocks);
            await screen.ShowAsync(CancellationToken.None);

            // Act
            screen.Dispose();

            // Assert
            _inputLock.Received(1).Dispose();
        }

        private sealed class FakeLoadingScreenView : ILoadingScreenView
        {
            public List<bool> VisibilitySets { get; } = new();
            public List<bool> Fades { get; } = new();
            public UniTask FadeIn { get; set; } = UniTask.CompletedTask;
            public UniTask FadeOut { get; set; } = UniTask.CompletedTask;

            public void SetVisible(bool isVisible)
            {
                VisibilitySets.Add(isVisible);
            }

            public UniTask FadeAsync(bool isVisible, CancellationToken ct)
            {
                Fades.Add(isVisible);
                return isVisible ? FadeIn : FadeOut;
            }
        }
    }
}
