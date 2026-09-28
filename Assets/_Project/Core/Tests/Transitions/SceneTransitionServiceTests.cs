using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Domains;
using Core.Input;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace Core.Tests.Transitions
{
    public sealed class SceneTransitionServiceTests
    {
        private ITransitionOverlayView _overlay = null!;
        private ILockService<InputLockTag> _inputLocks = null!;
        private ILock<InputLockTag> _inputLock = null!;
        private SceneTransitionService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _overlay = Substitute.For<ITransitionOverlayView>();
            _inputLocks = Substitute.For<ILockService<InputLockTag>>();
            _inputLock = Substitute.For<ILock<InputLockTag>>();
            _inputLocks.LockAll().Returns(_inputLock);
            _overlay.FadeInAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            _overlay.FadeOutAsync(Arg.Any<CancellationToken>()).Returns(UniTask.CompletedTask);
            _service = new SceneTransitionService(_overlay, _inputLocks);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public async Task ShowAsync_None_DoesNothing()
        {
            // Act
            await _service.ShowAsync(Transition.None, CancellationToken.None);

            // Assert
            _ = _overlay.DidNotReceiveWithAnyArgs().FadeInAsync(default);
            _inputLocks.DidNotReceive().LockAll();
        }

        [Test]
        public async Task HideAsync_None_DoesNothing()
        {
            // Act
            await _service.HideAsync(Transition.None, CancellationToken.None);

            // Assert
            _ = _overlay.DidNotReceiveWithAnyArgs().FadeOutAsync(default);
        }

        [Test]
        public async Task ShowAsync_Fade_FadesInAndLocksAllInput()
        {
            // Act
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(1).FadeInAsync(default);
            _inputLocks.Received(1).LockAll();
            _inputLock.DidNotReceive().Dispose();
        }

        [Test]
        public void ShowAsync_FadeInRunning_CompletesWhenFadeInCompletes()
        {
            // Arrange
            var fadeIn = new UniTaskCompletionSource();
            _overlay.FadeInAsync(Arg.Any<CancellationToken>()).Returns(fadeIn.Task);

            // Act
            var show = _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Assert
            show.Status.Should().Be(UniTaskStatus.Pending);
            fadeIn.TrySetResult();
            show.Status.Should().Be(UniTaskStatus.Succeeded);
        }

        [Test]
        public async Task ShowAsync_Nested_FadesInAndLocksOnce()
        {
            // Act
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(1).FadeInAsync(default);
            _inputLocks.Received(1).LockAll();
        }

        [Test]
        public void ShowAsync_NestedWhileFadingIn_WaitsForTheSameFadeIn()
        {
            // Arrange
            var fadeIn = new UniTaskCompletionSource();
            _overlay.FadeInAsync(Arg.Any<CancellationToken>()).Returns(fadeIn.Task);
            var first = _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Act
            var second = _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Assert
            second.Status.Should().Be(UniTaskStatus.Pending);
            fadeIn.TrySetResult();
            first.Status.Should().Be(UniTaskStatus.Succeeded);
            second.Status.Should().Be(UniTaskStatus.Succeeded);
        }

        [Test]
        public async Task HideAsync_AfterSingleShow_FadesOutAndReleasesInputLock()
        {
            // Arrange
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Act
            await _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(1).FadeOutAsync(default);
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public async Task HideAsync_OtherShowStillActive_KeepsOverlayAndLock()
        {
            // Arrange
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Act
            await _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.DidNotReceiveWithAnyArgs().FadeOutAsync(default);
            _inputLock.DidNotReceive().Dispose();
        }

        [Test]
        public async Task HideAsync_LastOfNestedShows_FadesOutOnce()
        {
            // Arrange
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);
            await _service.ShowAsync(Transition.Fade, CancellationToken.None);
            await _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Act
            await _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(1).FadeOutAsync(default);
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public void HideAsync_FadeOutRunning_KeepsInputLockedUntilFadedOut()
        {
            // Arrange
            var fadeOut = new UniTaskCompletionSource();
            _overlay.FadeOutAsync(Arg.Any<CancellationToken>()).Returns(fadeOut.Task);
            _service.ShowAsync(Transition.Fade, CancellationToken.None).Forget();

            // Act
            var hide = _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Assert
            hide.Status.Should().Be(UniTaskStatus.Pending);
            _inputLock.DidNotReceive().Dispose();
            fadeOut.TrySetResult();
            hide.Status.Should().Be(UniTaskStatus.Succeeded);
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public void ShowAsync_WhileFadingOut_FadesInAgainAndKeepsLock()
        {
            // Arrange
            var fadeOut = new UniTaskCompletionSource();
            _overlay.FadeOutAsync(Arg.Any<CancellationToken>()).Returns(fadeOut.Task);
            _service.ShowAsync(Transition.Fade, CancellationToken.None).Forget();
            _service.HideAsync(Transition.Fade, CancellationToken.None).Forget();

            // Act
            _service.ShowAsync(Transition.Fade, CancellationToken.None).Forget();
            fadeOut.TrySetResult();

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(2).FadeInAsync(default);
            _inputLocks.Received(1).LockAll();
            _inputLock.DidNotReceive().Dispose();
        }

        [Test]
        public async Task ShowAsync_CancelledToken_StillCountsSoMatchingHideFadesOut()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Func<Task> show = () => _service.ShowAsync(Transition.Fade, cts.Token).AsTask();
            await show.Should().ThrowAsync<OperationCanceledException>();

            // Act
            await _service.HideAsync(Transition.Fade, CancellationToken.None);

            // Assert
            _ = _overlay.ReceivedWithAnyArgs(1).FadeOutAsync(default);
            _inputLock.Received(1).Dispose();
        }

        [Test]
        public async Task HideAsync_WithoutShow_Throws()
        {
            // Act
            Func<Task> act = () => _service.HideAsync(Transition.Fade, CancellationToken.None).AsTask();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task Dispose_WhileShown_ReleasesInputLock()
        {
            // Arrange
            var service = new SceneTransitionService(_overlay, _inputLocks);
            await service.ShowAsync(Transition.Fade, CancellationToken.None);

            // Act
            service.Dispose();

            // Assert
            _inputLock.Received(1).Dispose();
        }
    }
}
