using System.Collections.Generic;
using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;

namespace Core.Content
{
    public sealed class ContentLoader : IContentLoader
    {
        private readonly Dictionary<object, int> _references = new();

        public async UniTask<OneOf<T, NotFound>> LoadAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object
        {
            ct.ThrowIfCancellationRequested();

            if (!loadable.LoadableObjectId.IsValid)
            {
                return new NotFound();
            }

            AddReference(loadable);
            var isLoaded = false;

            try
            {
                var asset = await LoadOrWaitAsync(loadable, ct);
                ct.ThrowIfCancellationRequested();

                if (asset == null)
                {
                    return new NotFound();
                }

                isLoaded = true;
                return asset;
            }
            finally
            {
                if (!isLoaded)
                {
                    Release(loadable);
                }
            }
        }

        public void Release<T>(Loadable<T> loadable) where T : UnityEngine.Object
        {
            if (!_references.TryGetValue(loadable, out var references))
            {
                return;
            }

            if (references > 1)
            {
                _references[loadable] = references - 1;
                return;
            }

            _references.Remove(loadable);

            if (loadable.Status != LoadableStatus.None)
            {
                loadable.Release();
            }
        }

        private static async UniTask<T?> LoadOrWaitAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object
        {
            if (loadable.Status is not (LoadableStatus.Loading or LoadableStatus.Loaded))
            {
                return await loadable.LoadAsync().AsUniTask();
            }

            await UniTask.WaitWhile(() => loadable.Status == LoadableStatus.Loading, cancellationToken: ct);
            return loadable.Status == LoadableStatus.Loaded ? loadable.Target : null;
        }

        private void AddReference(object loadable)
        {
            _references.TryGetValue(loadable, out var references);
            _references[loadable] = references + 1;
        }
    }
}
