using System;
using System.Collections.Generic;
using System.Threading;
using Core.Content;
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

        private readonly ISceneTransitionService _transitions;
        private readonly IContentDirectoryRegistry _contentDirectories;
        private readonly ISceneLoader _sceneLoader;
        private readonly HashSet<Type> _runningDescriptorTypes = new();
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        public DomainRunner(ISceneTransitionService transitions, IContentDirectoryRegistry contentDirectories, ISceneLoader sceneLoader)
        {
            _transitions = transitions;
            _contentDirectories = contentDirectories;
            _sceneLoader = sceneLoader;
        }

        public async UniTask<TResult> RunAsync<TArgs, TResult>(DomainDescriptor descriptor, ScopeRef parent, TArgs args, Transition transition, CancellationToken ct)
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

            var isTransitionShown = false;
            Scene? scopeScene = null;
            DomainLifetimeScope? scope = null;

            try
            {
                Log.Info(descriptor.LogTag, "Starting.");
                isTransitionShown = true;
                await _transitions.ShowAsync(transition, ct);

                var completion = new DomainCompletion<TResult>();
                await _loadGate.WaitAsync(ct);

                try
                {
                    var content = GetContent(descriptor);
                    scopeScene = await LoadScopeSceneAsync(descriptor, content, parent, args, completion);
                    scope = FindScope(descriptor, scopeScene.Value);
                }
                finally
                {
                    _loadGate.Release();
                }

                ct.ThrowIfCancellationRequested();
                isTransitionShown = false;
                await _transitions.HideAsync(transition, ct);

                return await completion.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                await TearDownAsync(descriptor, scope, scopeScene, transition, isTransitionShown);
            }
        }

        private DomainContent GetContent(DomainDescriptor descriptor)
        {
            return _contentDirectories.GetContent(descriptor).Match(
                content => content,
                _ => throw new InvalidOperationException($"No content root found for {descriptor.GetType().Name} (content directory '{descriptor.ContentDirectoryName}')."));
        }

        private async UniTask<Scene> LoadScopeSceneAsync<TArgs, TResult>(DomainDescriptor descriptor, DomainContent content, ScopeRef parent, TArgs args, DomainCompletion<TResult> completion)
            where TArgs : class
            where TResult : class
        {
            OneOf<Scene, NotFound> result;

            using (LifetimeScope.EnqueueParent(parent.Scope))
            using (LifetimeScope.Enqueue(builder => InstallDomainBindings(builder, content, args, completion)))
            {
                result = await _sceneLoader.LoadAdditiveAsync(content.ScopeScene, CancellationToken.None);
            }

            return result.Match(
                scene => scene,
                _ => throw new InvalidOperationException($"Scope scene of {descriptor.GetType().Name} not found in content directory '{descriptor.ContentDirectoryName}'."));
        }

        private static void InstallDomainBindings<TArgs, TResult>(IContainerBuilder builder, DomainContent content, TArgs args, DomainCompletion<TResult> completion)
            where TArgs : class
            where TResult : class
        {
            builder.RegisterInstance(args);
            builder.RegisterInstance(content, content.GetType());
            builder.RegisterInstance(completion);
        }

        private static DomainLifetimeScope FindScope(DomainDescriptor descriptor, Scene scopeScene)
        {
            var scope = LifetimeScope.Find<DomainLifetimeScope>(scopeScene) as DomainLifetimeScope;

            if (scope == null)
            {
                throw new InvalidOperationException($"Scope scene '{scopeScene.name}' of {descriptor.GetType().Name} has no {nameof(DomainLifetimeScope)}.");
            }

            if (scope.Container == null)
            {
                throw new InvalidOperationException($"{scope.GetType().Name} in scene '{scopeScene.name}' failed to build.");
            }

            return scope;
        }

        private async UniTask TearDownAsync(DomainDescriptor descriptor, DomainLifetimeScope? scope, Scene? scopeScene, Transition transition, bool isTransitionShown)
        {
            try
            {
                if (scope != null)
                {
                    var sceneSet = scope.Container.Resolve<DomainSceneSet>();
                    scope.Dispose();
                    await sceneSet.UnloadAllAsync(CancellationToken.None);
                }

                if (scopeScene.HasValue)
                {
                    await _sceneLoader.UnloadAsync(scopeScene.Value, CancellationToken.None);
                }

                if (isTransitionShown)
                {
                    await _transitions.HideAsync(transition, CancellationToken.None);
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
    }
}
