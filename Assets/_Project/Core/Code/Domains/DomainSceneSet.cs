using System;
using System.Collections.Generic;
using System.Threading;
using Core.Content;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using R3;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Domains
{
    public sealed class DomainSceneSet : IDisposable
    {
        public Observable<Scene> SceneLoaded => _sceneLoaded;

        private int _pendingLoadCount;
        private UniTaskCompletionSource? _loadsSettled;

        private readonly ISceneLoader _sceneLoader;
        private readonly List<Scene> _loadedScenes = new();
        private readonly Subject<Scene> _sceneLoaded = new();

        public DomainSceneSet(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public async UniTask<OneOf<Scene, NotFound>> LoadAsync(LoadableSceneId id, CancellationToken ct)
        {
            _pendingLoadCount++;

            try
            {
                var result = await _sceneLoader.LoadAdditiveAsync(id, ct);

                if (result.TryPickT0(out var scene, out _))
                {
                    _loadedScenes.Add(scene);
                    _sceneLoaded.OnNext(scene);
                }

                return result;
            }
            finally
            {
                _pendingLoadCount--;

                if (_pendingLoadCount == 0)
                {
                    _loadsSettled?.TrySetResult();
                    _loadsSettled = null;
                }
            }
        }

        public async UniTask UnloadAsync(Scene scene, CancellationToken ct)
        {
            if (!_loadedScenes.Remove(scene))
            {
                throw new InvalidOperationException($"Scene '{scene.name}' was not loaded through this {nameof(DomainSceneSet)}.");
            }

            await _sceneLoader.UnloadAsync(scene, ct);
        }

        internal async UniTask WaitForPendingLoadsAsync(CancellationToken ct)
        {
            if (_pendingLoadCount > 0)
            {
                _loadsSettled ??= new UniTaskCompletionSource();
                await _loadsSettled.Task.AttachExternalCancellation(ct);
            }
        }

        internal async UniTask UnloadAllAsync(CancellationToken ct)
        {
            await WaitForPendingLoadsAsync(CancellationToken.None);

            for (var i = _loadedScenes.Count - 1; i >= 0; i--)
            {
                var scene = _loadedScenes[i];
                _loadedScenes.RemoveAt(i);
                await _sceneLoader.UnloadAsync(scene, ct);
            }
        }

        public void Dispose()
        {
            _sceneLoaded.Dispose();
        }
    }
}
