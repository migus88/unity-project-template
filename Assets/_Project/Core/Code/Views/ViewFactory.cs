using System;
using System.Collections.Generic;
using System.Threading;
using Core.Content;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Views
{
    public sealed class ViewFactory : IViewFactory
    {
        private readonly IContentLoader _contentLoader;
        private readonly Dictionary<Loadable<GameObject>, int> _prefabUsages = new();
        private readonly Dictionary<Component, Loadable<GameObject>> _prefabsByView = new();

        public ViewFactory(IContentLoader contentLoader)
        {
            _contentLoader = contentLoader;
        }

        public async UniTask<OneOf<TView, NotFound>> CreateAsync<TView>(Loadable<GameObject> prefab, Transform parent, CancellationToken ct)
            where TView : Component
        {
            ct.ThrowIfCancellationRequested();
            AcquirePrefab(prefab);
            var isCreated = false;

            try
            {
                var loaded = await _contentLoader.LoadAsync(prefab, CancellationToken.None);
                ct.ThrowIfCancellationRequested();

                if (!loaded.TryPickT0(out var prefabObject, out var notFound))
                {
                    return notFound;
                }

                var view = await InstantiateAsync<TView>(prefabObject, parent, ct);
                _prefabsByView.Add(view, prefab);
                isCreated = true;
                return view;
            }
            finally
            {
                if (!isCreated)
                {
                    ReleasePrefab(prefab);
                }
            }
        }

        public void Destroy<TView>(TView view) where TView : Component
        {
            if (!_prefabsByView.Remove(view, out var prefab))
            {
                throw new InvalidOperationException($"View {typeof(TView).Name} was not created by this factory or was already destroyed.");
            }

            if (view != null)
            {
                Object.Destroy(view.gameObject);
            }

            ReleasePrefab(prefab);
        }

        private static async UniTask<TView> InstantiateAsync<TView>(GameObject prefab, Transform parent, CancellationToken ct)
            where TView : Component
        {
            var instances = await Object.InstantiateAsync(prefab, parent).ToUniTask();
            var instance = instances[0];

            if (ct.IsCancellationRequested)
            {
                Object.Destroy(instance);
                throw new OperationCanceledException(ct);
            }

            if (!instance.TryGetComponent<TView>(out var view))
            {
                Object.Destroy(instance);
                throw new InvalidOperationException($"Prefab '{prefab.name}' has no {typeof(TView).Name} component on its root.");
            }

            return view;
        }

        private void AcquirePrefab(Loadable<GameObject> prefab)
        {
            _prefabUsages.TryGetValue(prefab, out var usages);
            _prefabUsages[prefab] = usages + 1;
        }

        private void ReleasePrefab(Loadable<GameObject> prefab)
        {
            var usages = _prefabUsages[prefab] - 1;

            if (usages > 0)
            {
                _prefabUsages[prefab] = usages;
                return;
            }

            _prefabUsages.Remove(prefab);
            _contentLoader.Release(prefab);
        }
    }
}
