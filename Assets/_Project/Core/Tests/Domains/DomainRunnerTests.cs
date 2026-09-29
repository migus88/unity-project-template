using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Domains;
using Core.Localization;
using Core.Results;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using OneOf;
using R3;
using TestUtils;
using Unity.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Core.Tests.Domains
{
    public sealed class DomainRunnerTests
    {
        private ILoadingScreen _loadingScreen = null!;
        private IContentDirectoryRegistry _contentDirectories = null!;
        private ISceneLoader _sceneLoader = null!;
        private DomainRunner _runner = null!;
        private FirstTestDomainDescriptor _firstDescriptor = null!;
        private SecondTestDomainDescriptor _secondDescriptor = null!;
        private TestDomainContent _content = null!;
        private CancellationTokenSource _cts = null!;
        private EmptyDomainScope? _loadedScope;

        private readonly List<GameObject> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _loadingScreen = Substitute.For<ILoadingScreen>();
            _contentDirectories = Substitute.For<IContentDirectoryRegistry>();
            _sceneLoader = Substitute.For<ISceneLoader>();
            _runner = new DomainRunner(_loadingScreen, _contentDirectories, _sceneLoader);
            _firstDescriptor = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();
            _secondDescriptor = ScriptableObject.CreateInstance<SecondTestDomainDescriptor>();
            _content = ScriptableObject.CreateInstance<TestDomainContent>();
            _cts = new CancellationTokenSource();
            _loadedScope = null;

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

            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    createdObject.GetComponent<LifetimeScope>().DisposeCore();
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public async Task RunAsync_ParentDepthTwo_ThrowsAndHidesLoadingScreenWithoutShowingIt()
        {
            // Arrange
            var parent = new ScopeRef(null!, 2);

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _loadingScreen.DidNotReceiveWithAnyArgs().ShowAsync(default);
            _ = _loadingScreen.Received(1).HideAsync(CancellationToken.None);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RunAsync_ParentDepthBelowTwo_ShowsLoadingScreen(int parentDepth)
        {
            // Arrange
            ShowLoadingScreenUntilCancelled();

            // Act
            var run = Run(_firstDescriptor, new ScopeRef(null!, parentDepth), _cts.Token);

            // Assert
            run.IsCompleted.Should().BeFalse();
            _ = _loadingScreen.Received(1).ShowAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RunAsync_SameDescriptorTypeAlreadyRunning_Throws()
        {
            // Arrange
            ShowLoadingScreenUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);
            var otherInstanceOfSameType = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();

            try
            {
                // Act
                Func<Task> act = () => Run(otherInstanceOfSameType, RootScope(), CancellationToken.None);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>();
                firstRun.IsCompleted.Should().BeFalse();
                _ = _loadingScreen.Received(1).ShowAsync(Arg.Any<CancellationToken>());
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
            ShowLoadingScreenUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);

            // Act
            var secondRun = Run(_secondDescriptor, RootScope(), _cts.Token);

            // Assert
            firstRun.IsCompleted.Should().BeFalse();
            secondRun.IsCompleted.Should().BeFalse();
            _ = _loadingScreen.Received(2).ShowAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RunAsync_CancelledWhileShowingLoadingScreen_HidesItAndThrows()
        {
            // Arrange
            ShowLoadingScreenUntilCancelled();
            var run = Run(_firstDescriptor, RootScope(), _cts.Token);

            // Act
            _cts.Cancel();

            // Assert
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            _ = _loadingScreen.Received(1).HideAsync(CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_PreviousRunOfSameTypeCancelled_RunsAgain()
        {
            // Arrange
            ShowLoadingScreenUntilCancelled();
            var firstRun = Run(_firstDescriptor, RootScope(), _cts.Token);
            _cts.Cancel();
            await firstRun.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            using var secondCts = new CancellationTokenSource();

            // Act
            var secondRun = Run(_firstDescriptor, RootScope(), secondCts.Token);

            // Assert
            secondRun.IsCompleted.Should().BeFalse();
            _ = _loadingScreen.Received(2).ShowAsync(Arg.Any<CancellationToken>());
            secondCts.Cancel();
            await secondRun.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task RunAsync_ContentNotFound_ThrowsAndHidesLoadingScreen()
        {
            // Arrange
            _contentDirectories.GetContent(Arg.Any<DomainDescriptor>()).Returns((OneOf<DomainContent, NotFound>)new NotFound());

            // Act
            Func<Task> act = () => Run(_firstDescriptor, RootScope(), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().LoadAdditiveAsync(default, default);
            _ = _loadingScreen.Received(1).HideAsync(CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_ScopeSceneNotFound_ThrowsAndHidesLoadingScreen()
        {
            // Arrange
            var parent = RootScope();

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _ = _sceneLoader.Received(1).LoadAdditiveAsync(_content.ScopeScene, Arg.Any<CancellationToken>());
            _ = _sceneLoader.DidNotReceiveWithAnyArgs().UnloadAsync(default, default);
            _ = _loadingScreen.Received(1).HideAsync(CancellationToken.None);
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

        [Test]
        public async Task RunAsync_EntryPointThrows_ThrowsTearsDownScopeAndLeavesLoadingScreenHidden()
        {
            // Arrange
            var parent = CreateParentScope();
            FailingDomainScope? scope = null;
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    scope = CreateFailingScope();
                    return UniTask.FromResult<OneOf<Scene, NotFound>>(scope.gameObject.scene);
                });
            LogAssert.Expect(LogType.Exception, new Regex(FailingDomainScope.FailureMessage));
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingDomainScope.FailureMessage);
            scope!.Container.Should().BeNull();
            _ = _sceneLoader.Received(1).UnloadAsync(scope.gameObject.scene, CancellationToken.None);
            _ = _loadingScreen.Received(1).ShowAsync(Arg.Any<CancellationToken>());
            _ = _loadingScreen.Received(2).HideAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RunAsync_EntryPointCancels_DoesNotFailRun()
        {
            // Arrange
            var parent = CreateParentScope();
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    var scope = CreateScopeLikeAwake<CancellingDomainScope>();
                    return UniTask.FromResult<OneOf<Scene, NotFound>>(scope.gameObject.scene);
                });
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            // Act
            var run = Run(_firstDescriptor, parent, _cts.Token);
            var isRunningAfterBuild = !run.IsCompleted;
            _cts.Cancel();

            // Assert
            isRunningAfterBuild.Should().BeTrue();
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task RunAsync_EntryPointCannotBeResolved_ThrowsAndUnloadsScopeScene()
        {
            // Arrange
            var parent = CreateParentScope();
            UnresolvableDomainScope? scope = null;
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    scope = CreateScopeLikeAwake<UnresolvableDomainScope>();
                    return UniTask.FromResult<OneOf<Scene, NotFound>>(scope.gameObject.scene);
                });
            LogAssert.Expect(LogType.Exception, new Regex(nameof(VContainerException)));

            // Act
            Func<Task> act = () => Run(_firstDescriptor, parent, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*failed to build*");
            _ = _sceneLoader.Received(1).UnloadAsync(scope!.gameObject.scene, CancellationToken.None);
            _ = _loadingScreen.Received(1).HideAsync(CancellationToken.None);
        }

        [Test]
        public async Task RunAsync_LoadingScopeBuilt_ShowsLoadingScreenBeforeLoadAndHidesItOnceStarted()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            // Act
            var run = Run(_firstDescriptor, parent, _cts.Token);
            await UniTask.DelayFrame(2);

            // Assert
            run.IsCompleted.Should().BeFalse();
            Received.InOrder(() =>
            {
                _ = _loadingScreen.ShowAsync(Arg.Any<CancellationToken>());
                _ = _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>());
                _ = _loadingScreen.HideAsync(Arg.Any<CancellationToken>());
            });
            _cts.Cancel();
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task RunAsync_LoadingContentSceneStillLoading_HidesLoadingScreenOnceItLoads()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            var contentSceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId("Assets/_Project/Bootstrap/Scenes/Bootstrap.unity");
            var contentLoad = new UniTaskCompletionSource<OneOf<Scene, NotFound>>();
            _sceneLoader.LoadAdditiveAsync(contentSceneId, Arg.Any<CancellationToken>()).Returns(contentLoad.Task);
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            var run = Run(_firstDescriptor, parent, _cts.Token);
            _loadedScope!.Container.Resolve<DomainSceneSet>().LoadAsync(contentSceneId, CancellationToken.None).Forget();
            await UniTask.DelayFrame(2);
            var wasHiddenWhileLoading = _loadingScreen.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(ILoadingScreen.HideAsync));

            // Act
            contentLoad.TrySetResult(SceneManager.GetActiveScene());

            // Assert
            wasHiddenWhileLoading.Should().BeFalse();
            _ = _loadingScreen.Received(1).HideAsync(Arg.Any<CancellationToken>());
            _cts.Cancel();
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task RunAsync_LoadingDomainCompletes_CoversBeforeTeardownAndStaysCovered()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            var run = Run(_firstDescriptor, parent, _cts.Token);
            await UniTask.DelayFrame(2);

            // Act
            CompleteLoadedDomain();
            var result = await run;

            // Assert
            result.Value.Should().Be(1);
            Received.InOrder(() =>
            {
                _ = _loadingScreen.ShowAsync(Arg.Any<CancellationToken>());
                _ = _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>());
                _ = _loadingScreen.HideAsync(Arg.Any<CancellationToken>());
                _ = _loadingScreen.ShowAsync(Arg.Any<CancellationToken>());
                _ = _sceneLoader.UnloadAsync(Arg.Any<Scene>(), Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public async Task RunAsync_NoTransitionDomainCompletes_NeverTouchesLoadingScreen()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            var run = Run(_firstDescriptor, parent, _cts.Token, Transition.None);

            // Act
            CompleteLoadedDomain();
            await run;

            // Assert
            _ = _sceneLoader.Received(1).UnloadAsync(Arg.Any<Scene>(), CancellationToken.None);
            _loadingScreen.ReceivedCalls().Should().BeEmpty();
        }

        [Test]
        public async Task RunAsync_NoTransitionFails_NeverTouchesLoadingScreen()
        {
            // Act
            Func<Task> act = () => Run(_firstDescriptor, RootScope(), CancellationToken.None, Transition.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            _loadingScreen.ReceivedCalls().Should().BeEmpty();
        }

        [Test]
        public async Task RunAsync_LoadingDomainCancelledWhileRunning_HidesLoadingScreenAfterTeardown()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            var run = Run(_firstDescriptor, parent, _cts.Token);
            await UniTask.DelayFrame(2);

            // Act
            _cts.Cancel();

            // Assert
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            _ = _loadingScreen.Received(1).ShowAsync(Arg.Any<CancellationToken>());
            Received.InOrder(() =>
            {
                _ = _loadingScreen.HideAsync(Arg.Any<CancellationToken>());
                _ = _sceneLoader.UnloadAsync(Arg.Any<Scene>(), Arg.Any<CancellationToken>());
                _ = _loadingScreen.HideAsync(CancellationToken.None);
            });
        }

        [Test]
        public async Task RunAsync_CancelledWhileCoveringFinishedDomain_TearsDownAndHidesLoadingScreen()
        {
            // Arrange
            var parent = CreateParentScope();
            BuildEmptyScopeOnSceneLoad();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            var run = Run(_firstDescriptor, parent, _cts.Token);
            await UniTask.DelayFrame(2);
            ShowLoadingScreenUntilCancelled();
            CompleteLoadedDomain();

            // Act
            _cts.Cancel();

            // Assert
            await run.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            Received.InOrder(() =>
            {
                _ = _loadingScreen.ShowAsync(Arg.Any<CancellationToken>());
                _ = _loadingScreen.ShowAsync(Arg.Any<CancellationToken>());
                _ = _sceneLoader.UnloadAsync(Arg.Any<Scene>(), Arg.Any<CancellationToken>());
                _ = _loadingScreen.HideAsync(CancellationToken.None);
            });
        }

        private Task<TestDomainResult> Run(DomainDescriptor descriptor, ScopeRef parent, CancellationToken ct, Transition transition = Transition.Loading)
        {
            return _runner.RunAsync<TestDomainArgs, TestDomainResult>(descriptor, parent, new TestDomainArgs(), transition, ct).AsTask();
        }

        private void ShowLoadingScreenUntilCancelled()
        {
            _loadingScreen.ShowAsync(Arg.Any<CancellationToken>())
                .Returns(call => UniTask.Never(call.ArgAt<CancellationToken>(0)));
        }

        private void BuildEmptyScopeOnSceneLoad()
        {
            _sceneLoader.LoadAdditiveAsync(Arg.Any<LoadableSceneId>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    _loadedScope = CreateScopeLikeAwake<EmptyDomainScope>();
                    return UniTask.FromResult<OneOf<Scene, NotFound>>(_loadedScope.gameObject.scene);
                });
        }

        private void CompleteLoadedDomain()
        {
            _loadedScope!.Container.Resolve<DomainCompletion<TestDomainResult>>().Complete(new TestDomainResult(1));
        }

        private ScopeRef CreateParentScope()
        {
            var parentObject = new GameObject("Parent Scope");
            _createdObjects.Add(parentObject);
            var parentScope = parentObject.AddComponent<LifetimeScope>();
            var localization = Substitute.For<ILocalizationService>();
            localization.Current.Returns(new ReactiveProperty<Language>(Language.English));
            var parent = new ScopeRef(parentScope, 0);

            using (LifetimeScope.Enqueue(builder =>
            {
                builder.RegisterInstance(parent);
                builder.RegisterInstance(_sceneLoader);
                builder.RegisterInstance(localization);
            }))
            {
                parentScope.Build();
            }

            return parent;
        }

        private FailingDomainScope CreateFailingScope()
        {
            var scopeObject = new GameObject("Failing Scope");
            _createdObjects.Add(scopeObject);
            var scope = scopeObject.AddComponent<FailingDomainScope>();
            scope.Build();
            return scope;
        }

        private TScope CreateScopeLikeAwake<TScope>() where TScope : LifetimeScope
        {
            var scopeObject = new GameObject(typeof(TScope).Name);
            _createdObjects.Add(scopeObject);
            var scope = scopeObject.AddComponent<TScope>();

            try
            {
                scope.Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return scope;
        }

        private static ScopeRef RootScope()
        {
            return new ScopeRef(null!, 0);
        }
    }
}
