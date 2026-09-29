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
        private readonly Dictionary<Component, Loadable<GameObject>> _prefabsByView = new();

        public ViewFactory(IContentLoader contentLoader)
        {
            _contentLoader = contentLoader;
        }

        public async UniTask<OneOf<TView, NotFound>> CreateAsync<TView>(Loadable<GameObject> prefab, Transform parent, CancellationToken ct)
            where TView : Component
        {
            ct.ThrowIfCancellationRequested();
            var loaded = await _contentLoader.LoadAsync(prefab, ct);

            if (!loaded.TryPickT0(out var prefabObject, out var notFound))
            {
                return notFound;
            }

            var isCreated = false;

            try
            {
                ct.ThrowIfCancellationRequested();
                var view = await InstantiateAsync<TView>(prefabObject, parent, ct);
                _prefabsByView.Add(view, prefab);
                isCreated = true;
                return view;
            }
            finally
            {
                if (!isCreated)
                {
                    _contentLoader.Release(prefab);
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

            _contentLoader.Release(prefab);
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
    }
}
