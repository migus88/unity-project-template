using System;
using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;

namespace Core.Content
{
    public sealed class ContentLoader : IContentLoader
    {
        public async UniTask<OneOf<T, NotFound>> LoadAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object
        {
            ct.ThrowIfCancellationRequested();

            if (!loadable.LoadableObjectId.IsValid)
            {
                return new NotFound();
            }

            if (loadable.Status is LoadableStatus.Loading or LoadableStatus.Loaded)
            {
                return await WaitForSharedLoadAsync(loadable, ct);
            }

            var asset = await loadable.LoadAsync().AsUniTask();

            if (ct.IsCancellationRequested)
            {
                loadable.Release();
                throw new OperationCanceledException(ct);
            }

            if (asset == null)
            {
                return new NotFound();
            }

            return asset;
        }

        public void Release<T>(Loadable<T> loadable) where T : UnityEngine.Object
        {
            if (loadable.Status == LoadableStatus.None)
            {
                return;
            }

            loadable.Release();
        }

        private static async UniTask<OneOf<T, NotFound>> WaitForSharedLoadAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object
        {
            await UniTask.WaitWhile(() => loadable.Status == LoadableStatus.Loading, cancellationToken: ct);

            if (loadable.Status != LoadableStatus.Loaded || loadable.Target == null)
            {
                return new NotFound();
            }

            return loadable.Target;
        }
    }
}
