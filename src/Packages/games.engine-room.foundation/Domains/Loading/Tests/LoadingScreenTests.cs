using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Input;
using Cysharp.Threading.Tasks;
using Migs.MLock;
using Migs.MLock.Interfaces;
using NUnit.Framework;

namespace Loading.Tests
{
    public sealed class LoadingScreenTests
    {
        private ILockService<InputLockTag> _inputLocks = null!;
        private List<bool> _lockChanges = null!;
        private LockGate<InputLockTag> _probe = null!;
        private FakeLoadingScreenView _view = null!;
        private LoadingScreen _screen = null!;

        [SetUp]
        public void SetUp()
        {
            _inputLocks = new BaseLockService<InputLockTag>();
            _lockChanges = new List<bool>();
            _probe = new LockGate<InputLockTag>(_inputLocks, InputLockTag.Movement, _lockChanges.Add);
            _view = new FakeLoadingScreenView();
            _screen = new LoadingScreen(_inputLocks);
        }

        [TearDown]
        public void TearDown()
        {
            _screen.Dispose();
            _probe.Dispose();
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
            _lockChanges.Should().Equal(true);
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
            _lockChanges.Should().Equal(true, false);
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
            _probe.IsLocked.Should().BeTrue();
            _lockChanges.Should().Equal(true);
        }

        [Test]
        public void HideAsync_WhileFadingIn_KeepsInputLockedUntilFadeOutEnds()
        {
            // Arrange
            var fadeIn = new UniTaskCompletionSource();
            var fadeOut = new UniTaskCompletionSource();
            _view.FadeIn = fadeIn.Task;
            _view.FadeOut = fadeOut.Task;
            _screen.Attach(_view);
            _screen.ShowAsync(CancellationToken.None).Forget();
            var hide = _screen.HideAsync(CancellationToken.None);

            // Act
            fadeIn.TrySetResult();

            // Assert
            _probe.IsLocked.Should().BeTrue();
            hide.Status.IsCompleted().Should().BeFalse();
            fadeOut.TrySetResult();
            _lockChanges.Should().Equal(true, false);
            hide.Status.Should().Be(UniTaskStatus.Succeeded);
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
            _lockChanges.Should().BeEmpty();
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
            _lockChanges.Should().Equal(true);
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
            _lockChanges.Should().Equal(true, false);
        }

        [Test]
        public async Task HideAsync_AfterDisposeWhileShown_CompletesWithoutFading()
        {
            // Arrange
            var screen = new LoadingScreen(_inputLocks);
            screen.Attach(_view);
            await screen.ShowAsync(CancellationToken.None);
            screen.Dispose();

            // Act
            Func<Task> act = () => screen.HideAsync(CancellationToken.None).AsTask();

            // Assert
            await act.Should().NotThrowAsync();
            _view.Fades.Should().Equal(true);
            _lockChanges.Should().Equal(true, false);
        }

        [Test]
        public async Task ShowAsync_AfterDispose_DoesNotLockInput()
        {
            // Arrange
            var screen = new LoadingScreen(_inputLocks);
            screen.Dispose();

            // Act
            await screen.ShowAsync(CancellationToken.None);

            // Assert
            _lockChanges.Should().BeEmpty();
        }

        [Test]
        public void WaitForViewAsync_ViewAlreadyAttached_CompletesImmediately()
        {
            // Arrange
            _screen.Attach(_view);

            // Act
            var wait = _screen.WaitForViewAsync(CancellationToken.None);

            // Assert
            wait.Status.Should().Be(UniTaskStatus.Succeeded);
        }

        [Test]
        public async Task WaitForViewAsync_ScreenDisposed_ThrowsOperationCanceled()
        {
            // Arrange
            var screen = new LoadingScreen(_inputLocks);
            var wait = screen.WaitForViewAsync(CancellationToken.None);

            // Act
            screen.Dispose();
            Func<Task> act = () => wait.AsTask();

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task ShowAsync_BeforeViewAttached_AttachShowsViewBeforeWaitCompletes()
        {
            // Arrange
            await _screen.ShowAsync(CancellationToken.None);
            var visibilityAtCompletion = new List<bool>();
            _screen.WaitForViewAsync(CancellationToken.None).ContinueWith(() => visibilityAtCompletion.AddRange(_view.VisibilitySets)).Forget();

            // Act
            _screen.Attach(_view);

            // Assert
            visibilityAtCompletion.Should().Equal(true);
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
