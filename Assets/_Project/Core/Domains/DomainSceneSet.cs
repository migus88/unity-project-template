using System;
using System.Collections.Generic;
using System.Threading;
using Core.Content;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Domains
{
    public sealed class DomainSceneSet
    {
        private readonly ISceneLoader _sceneLoader;
        private readonly List<Scene> _loadedScenes = new();

        public DomainSceneSet(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public async UniTask<OneOf<Scene, NotFound>> LoadAsync(LoadableSceneId id, CancellationToken ct)
        {
            var result = await _sceneLoader.LoadAdditiveAsync(id, ct);

            if (result.TryPickT0(out var scene, out _))
            {
                _loadedScenes.Add(scene);
            }

            return result;
        }

        public async UniTask UnloadAsync(Scene scene, CancellationToken ct)
        {
            if (!_loadedScenes.Remove(scene))
            {
                throw new InvalidOperationException($"Scene '{scene.name}' was not loaded through this {nameof(DomainSceneSet)}.");
            }

            await _sceneLoader.UnloadAsync(scene, ct);
        }

        internal async UniTask UnloadAllAsync(CancellationToken ct)
        {
            for (var i = _loadedScenes.Count - 1; i >= 0; i--)
            {
                var scene = _loadedScenes[i];
                _loadedScenes.RemoveAt(i);
                await _sceneLoader.UnloadAsync(scene, ct);
            }
        }
    }
}
