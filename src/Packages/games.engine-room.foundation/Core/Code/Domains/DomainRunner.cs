using System;
using System.Collections.Generic;
using System.Threading;
using Core.Content;
using Core.Localization;
using Core.Logging;
using Core.Results;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using OneOf;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Core.Domains
{
    public sealed class DomainRunner : IDisposable
    {
        private const int MaxParentDepth = 1;

        private readonly ILoadingScreen _loadingScreen;
        private readonly IContentDirectoryRegistry _contentDirectories;
        private readonly ISceneLoader _sceneLoader;
        private readonly HashSet<Type> _runningDescriptorTypes = new();
        private readonly Dictionary<LifetimeScope, PendingStartups> _pendingStartups = new();
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        public DomainRunner(ILoadingScreen loadingScreen, IContentDirectoryRegistry contentDirectories, ISceneLoader sceneLoader)
        {
            _loadingScreen = loadingScreen;
            _contentDirectories = contentDirectories;
            _sceneLoader = sceneLoader;
        }

        public async UniTask<TResult> RunAsync<TArgs, TResult>(DomainDescriptor descriptor, ScopeRef parent, TArgs args, Transition transition, CancellationToken ct)
            where TArgs : class
            where TResult : class
        {
            var hasResult = false;

            try
            {
                var result = await RunDomainAsync<TArgs, TResult>(descriptor, parent, args, transition, ct);
                hasResult = true;
                return result;
            }
            finally
            {
                if (!hasResult)
                {
                    await HideLoadingScreenAsync(transition, CancellationToken.None);
                }
            }
        }

        private async UniTask<TResult> RunDomainAsync<TArgs, TResult>(DomainDescriptor descriptor, ScopeRef parent, TArgs args, Transition transition, CancellationToken ct)
            where TArgs : class
            where TResult : class
        {
            var descriptorType = descriptor.GetType();

            if (parent.Depth > MaxParentDepth)
            {
                throw new InvalidOperationException($"Cannot run {descriptorType.Name} under a scope of depth {parent.Depth}. Domains nest at most root -> main -> sub or leaf.");
            }

            if (!_runningDescriptorTypes.Add(descriptorType))
            {
                throw new InvalidOperationException($"{descriptorType.Name} is already running. Only one instance per domain type may run at a time.");
            }

            Scene? scopeScene = null;
            DomainLifetimeScope? scope = null;
            var parentStartups = TrackStartup(parent);
            var isStartupSettled = parentStartups == null;

            try
            {
                Log.Info(descriptor.LogTag, "Starting.");
                await ShowLoadingScreenAsync(transition, ct);

                var completion = new DomainCompletion<TResult>();
                await _loadGate.WaitAsync(ct);

                try
                {
                    var content = GetContent(descriptor);
                    var build = new ScopeBuild();
                    scopeScene = await LoadScopeSceneAsync(descriptor, content, parent, args, completion, build, CancellationToken.None);
                    scope = FindScope(descriptor, scopeScene.Value, build);
                }
                finally
                {
                    _loadGate.Release();
                }

                ct.ThrowIfCancellationRequested();

                if (transition == Transition.Loading || !isStartupSettled)
                {
                    await WaitUntilReadyAsync(scope, ct);
                }

                if (!isStartupSettled)
                {
                    isStartupSettled = true;
                    parentStartups!.Settle();
                }

                if (completion.Task.Status == UniTaskStatus.Pending)
                {
                    await HideLoadingScreenAsync(transition, ct);
                }

                var result = await completion.Task.AttachExternalCancellation(ct);
                await ShowLoadingScreenAsync(transition, ct);
                return result;
            }
            finally
            {
                if (!isStartupSettled)
                {
                    parentStartups!.Settle();
                }

                await TearDownAsync(descriptor, scope, scopeScene, CancellationToken.None);
            }
        }

        private PendingStartups? TrackStartup(ScopeRef parent)
        {
            if (parent.Scope is not DomainLifetimeScope parentScope)
            {
                return null;
            }

            if (!_pendingStartups.TryGetValue(parentScope, out var startups))
            {
                startups = new PendingStartups();
                _pendingStartups.Add(parentScope, startups);
            }

            startups.Add();
            return startups;
        }

        private async UniTask WaitUntilReadyAsync(DomainLifetimeScope scope, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            var sceneSet = scope.Container.Resolve<DomainSceneSet>();

            while (sceneSet.HasPendingLoads || HasPendingStartups(scope))
            {
                await sceneSet.WaitForPendingLoadsAsync(ct);

                if (_pendingStartups.TryGetValue(scope, out var startups))
                {
                    await startups.WaitAsync(ct);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private bool HasPendingStartups(DomainLifetimeScope scope)
        {
            return _pendingStartups.TryGetValue(scope, out var startups) && startups.IsPending;
        }

        private UniTask ShowLoadingScreenAsync(Transition transition, CancellationToken ct)
        {
            return transition == Transition.Loading ? _loadingScreen.ShowAsync(ct) : UniTask.CompletedTask;
        }

        private UniTask HideLoadingScreenAsync(Transition transition, CancellationToken ct)
        {
            return transition == Transition.Loading ? _loadingScreen.HideAsync(ct) : UniTask.CompletedTask;
        }

        private DomainContent GetContent(DomainDescriptor descriptor)
        {
            return _contentDirectories.GetContent(descriptor).Match(
                content => content,
                _ => throw new InvalidOperationException($"No content root found for {descriptor.GetType().Name} (content directory '{descriptor.ContentDirectoryName}')."));
        }

        private async UniTask<Scene> LoadScopeSceneAsync<TArgs, TResult>(DomainDescriptor descriptor, DomainContent content, ScopeRef parent, TArgs args, DomainCompletion<TResult> completion, ScopeBuild build, CancellationToken ct)
            where TArgs : class
            where TResult : class
        {
            OneOf<Scene, NotFound> result;

            using (LifetimeScope.EnqueueParent(parent.Scope))
            using (LifetimeScope.Enqueue(builder => InstallDomainBindings(builder, content, args, completion, build)))
            {
                result = await _sceneLoader.LoadAdditiveAsync(content.ScopeScene, ct);
            }

            return result.Match(
                scene => scene,
                _ => throw new InvalidOperationException($"Scope scene of {descriptor.GetType().Name} not found in content directory '{descriptor.ContentDirectoryName}'."));
        }

        private static void InstallDomainBindings<TArgs, TResult>(IContainerBuilder builder, DomainContent content, TArgs args, DomainCompletion<TResult> completion, ScopeBuild build)
            where TArgs : class
            where TResult : class
        {
            builder.RegisterInstance(args);
            builder.RegisterInstance(content, content.GetType());
            builder.RegisterInstance(completion).As<IDomainCompletion>();
            builder.RegisterEntryPoint(CreateLabelBinder, Lifetime.Singleton);
            builder.RegisterBuildCallback(_ => build.MarkCompleted());
        }

        private static LocalizedLabelBinder CreateLabelBinder(IObjectResolver resolver)
        {
            var scopeScene = resolver.Resolve<ScopeRef>().Scope.gameObject.scene;
            return new LocalizedLabelBinder(resolver.Resolve<ILocalizationService>(), scopeScene.GetRootGameObjects(), resolver.Resolve<DomainSceneSet>().SceneLoaded);
        }

        private static DomainLifetimeScope FindScope(DomainDescriptor descriptor, Scene scopeScene, ScopeBuild build)
        {
            var scope = LifetimeScope.Find<DomainLifetimeScope>(scopeScene) as DomainLifetimeScope;

            if (scope == null)
            {
                throw new InvalidOperationException($"Scope scene '{scopeScene.name}' of {descriptor.GetType().Name} has no {nameof(DomainLifetimeScope)}.");
            }

            if (scope.Container == null || !build.IsCompleted)
            {
                throw new InvalidOperationException($"{scope.GetType().Name} in scene '{scopeScene.name}' failed to build.");
            }

            return scope;
        }

        private async UniTask TearDownAsync(DomainDescriptor descriptor, DomainLifetimeScope? scope, Scene? scopeScene, CancellationToken ct)
        {
            try
            {
                if (scope != null)
                {
                    _pendingStartups.Remove(scope);
                    var sceneSet = scope.Container?.Resolve<DomainSceneSet>();
                    scope.Dispose();

                    if (sceneSet != null)
                    {
                        await sceneSet.UnloadAllAsync(ct);
                    }
                }

                if (scopeScene.HasValue)
                {
                    await _sceneLoader.UnloadAsync(scopeScene.Value, ct);
                }
            }
            finally
            {
                _runningDescriptorTypes.Remove(descriptor.GetType());
                Log.Info(descriptor.LogTag, "Stopped.");
            }
        }

        public void Dispose()
        {
            _loadGate.Dispose();
        }

        private sealed class ScopeBuild
        {
            public bool IsCompleted { get; private set; }

            public void MarkCompleted()
            {
                IsCompleted = true;
            }
        }

        private sealed class PendingStartups
        {
            public bool IsPending => _count > 0;

            private int _count;
            private UniTaskCompletionSource? _settled;

            public void Add()
            {
                _count++;
            }

            public void Settle()
            {
                _count--;

                if (_count == 0)
                {
                    _settled?.TrySetResult();
                    _settled = null;
                }
            }

            public async UniTask WaitAsync(CancellationToken ct)
            {
                if (_count > 0)
                {
                    _settled ??= new UniTaskCompletionSource();
                    await _settled.Task.AttachExternalCancellation(ct);
                }
            }
        }
    }
}
