using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Domains;
using Core.Results;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Tests.Domains
{
    public sealed class DomainRunnerTests
    {
        private ISceneTransitionService _transitions = null!;
        private IContentDirectoryRegistry _contentDirectories = null!;
        private ISceneLoader _sceneLoader = null!;
        private DomainRunner _runner = null!;
        private FirstTestDomainDescriptor _firstDescriptor = null!;
        private SecondTestDomainDescriptor _secondDescriptor = null!;
        private TestDomainContent _content = null!;
        private CancellationTokenSource _cts = null!;

        [SetUp]
        public void SetUp()
        {
            _transitions = Substitute.For<ISceneTransitionService>();
            _contentDirectories = Substitute.For<IContentDirectoryRegistry>();
            _sceneLoader = Substitute.For<ISceneLoader>();
            _runner = new DomainRunner(_transitions, _contentDirectories, _sceneLoader);
            _firstDescriptor = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();
            _secondDescriptor = ScriptableObject.CreateInstance<SecondTestDomainDescriptor>();
            _content = ScriptableObject.CreateInstance<TestDomainContent>();
            _cts = new CancellationTokenSource();

            _contentDirectories.GetContent(Arg.Any<DomainDescriptor>()).Returns((OneOf<DomainContent, NotFound>)_content);
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<OneOf<Scene, NotFound>>(new NotFound()));
        }

        [TearDown]
        public void TearDown()
        {
            _cts.Cancel();
            _cts.Dispose();
            _runner.Dispose();
            UnityEngine.Object.DestroyImmediate(_firstDescriptor);
            UnityEngine.Object.DestroyImmediate(_secondDescriptor);
            UnityEngine.Object.DestroyImmediate(_content);
        }

        [Test]
        public async Task RunAsync_ParentDepthTwo_ThrowsWithoutShowingTransition()
        {
            // Arrange
            var parent = new ScopeRef(null!, 2);

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _transitions.DidNotReceiveWithAnyArgs().ShowAsync(default, default);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RunAsync_ParentDepthBelowTwo_ShowsTransition(int parentDepth)
        {
            // Arrange
            ShowTransitionUntilCancelled();

            // Act
            var run = Run(_firstDescriptor, new ScopeRef(null!, parentDepth), _cts.Token);

            // Assert
            run.IsCompleted.Should().BeFalse();
            _ = _transitions.Received(1).ShowAsync(Transition.Fade, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RunAsync_SameDescriptorTypeAlreadyRunning_Throws()
        {
            // Arrange
            ShowTransitionUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);
            var otherInstanceOfSameType = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();

            try
            {
                // Act
                Func<Task> act = () => Run(otherInstanceOfSameType, RootScope(), CancellationToken.None);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>();
                firstRun.IsCompleted.Should().BeFalse();
                _ = _transitions.Received(1).ShowAsync(Arg.Any<Transition>(), Arg.Any<CancellationToken>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherInstanceOfSameType);
            }
        }

        [Test]
        public void RunAsync_DifferentDescriptorTypeAlreadyRunning_StartsBoth()
        {
            // Arrange
            ShowTransitionUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);

            // Act
            var secondRun = Run(_secondDescriptor, RootScope(), _cts.Token);

            // Assert
            firstRun.IsCompleted.Should().BeFalse();
            secondRun.IsCompleted.Should().BeFalse();
            _ = _transitions.Received(2).ShowAsync(Arg.Any<Transition>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RunAsync_CancelledWhileShowingTransition_HidesTransitionAndThrows()
        {
            // Arrange
            ShowTransitionUntilCancelled();
            var run = Run(_firstDescriptor, RootScope(), _cts.Token);

            // Act
            _cts.Cancel();

            // Assert
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            _ = _transitions.Received(1).HideAsync(Transition.Fade, CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_PreviousRunOfSameTypeCancelled_RunsAgain()
        {
            // Arrange
            ShowTransitionUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);
            _cts.Cancel();
            await firstRun.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            using var secondCts = new CancellationTokenSource();

            // Act
            var secondRun = Run(_firstDescriptor, RootScope(), secondCts.Token);

            // Assert
            secondRun.IsCompleted.Should().BeFalse();
            _ = _transitions.Received(2).ShowAsync(Arg.Any<Transition>(), Arg.Any<CancellationToken>());
            secondCts.Cancel();
            await secondRun.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task RunAsync_ContentNotFound_ThrowsAndHidesTransition()
        {
            // Arrange
            _contentDirectories.GetContent(Arg.Any<DomainDescriptor>()).Returns((OneOf<DomainContent, NotFound>)new NotFound());

            // Act
            Func<Task> act = () => Run(_firstDescriptor, RootScope(), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().LoadAdditiveAsync(default, default);
            _ = _transitions.Received(1).HideAsync(Transition.Fade, CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_ScopeSceneNotFound_ThrowsAndHidesTransition()
        {
            // Arrange
            var parent = RootScope();

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.Received(1).LoadAdditiveAsync(_content.ScopeScene, Arg.Any<CancellationToken>());
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().UnloadAsync(default, default);
            _ = _transitions.Received(1).HideAsync(Transition.Fade, CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_PreviousRunOfSameTypeFailed_ReleasesGuardAndLoadGate()
        {
            // Arrange
            Func<Task> firstRun = () => Run(_firstDescriptor, RootScope(), CancellationToken.None);
            await firstRun.Should().ThrowAsync<InvalidOperationException>();

            // Act
            Func<Task> secondRun = () => Run(_firstDescriptor, RootScope(), CancellationToken.None);

            // Assert
            await secondRun.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.ReceivedWithAnyArgs(2).LoadAdditiveAsync(default, default);
        }

        private Task<TestDomainResult> Run(DomainDescriptor descriptor, ScopeRef parent, CancellationToken ct)
        {
            return _runner.RunAsync<TestDomainArgs, TestDomainResult>(descriptor, parent, new TestDomainArgs(), Transition.Fade, ct).AsTask();
        }

        private void ShowTransitionUntilCancelled()
        {
            _transitions.ShowAsync(Arg.Any<Transition>(), Arg.Any<CancellationToken>())
                .Returns(call => UniTask.Never(call.ArgAt<CancellationToken>(1)));
        }

        private static ScopeRef RootScope()
        {
            return new ScopeRef(null!, 0);
        }
    }
}
