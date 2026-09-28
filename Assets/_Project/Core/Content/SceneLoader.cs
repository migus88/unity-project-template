using System;
using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Content
{
    public sealed class SceneLoader : ISceneLoader
    {
        public async UniTask<OneOf<Scene, NotFound>> LoadAdditiveAsync(LoadableSceneId id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!id.IsValid)
            {
                return new NotFound();
            }

            var operation = SceneManager.LoadSceneAsync(id, new LoadSceneParameters(LoadSceneMode.Additive));

            if (operation == null)
            {
                return new NotFound();
            }

            await operation.ToUniTask();
            var scene = SceneManager.GetSceneByLoadableSceneId(id);

            if (ct.IsCancellationRequested)
            {
                await UnloadAsync(scene, CancellationToken.None);
                throw new OperationCanceledException(ct);
            }

            return scene;
        }

        public async UniTask UnloadAsync(Scene scene, CancellationToken ct)
        {
            var operation = SceneManager.UnloadSceneAsync(scene);

            if (operation == null)
            {
                throw new InvalidOperationException($"Scene '{scene.name}' cannot be unloaded because it is not loaded.");
            }

            await operation.ToUniTask(cancellationToken: ct);
        }
    }
}
