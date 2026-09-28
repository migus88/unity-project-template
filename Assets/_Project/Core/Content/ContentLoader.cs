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

            var wasRequestedBefore = loadable.Status is LoadableStatus.Loading or LoadableStatus.Loaded;
            var asset = await loadable.LoadAsync().AsUniTask();

            if (ct.IsCancellationRequested)
            {
                if (!wasRequestedBefore)
                {
                    loadable.Release();
                }

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
            loadable.Release();
        }
    }
}
