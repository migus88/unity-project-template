using System;
using System.Collections.Generic;
using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine;

namespace Core.Views
{
    internal sealed class ViewPool<TView> : IViewPool<TView> where TView : Component
    {
        private bool _isDisposed;

        private readonly IViewFactory _viewFactory;
        private readonly Loadable<GameObject> _prefab;
        private readonly Stack<TView> _idleViews = new();
        private readonly HashSet<TView> _rentedViews = new();
        private readonly CancellationTokenSource _lifetimeCts = new();

        public ViewPool(IViewFactory viewFactory, Loadable<GameObject> prefab)
        {
            _viewFactory = viewFactory;
            _prefab = prefab;
        }

        public async UniTask<OneOf<TView, NotFound>> RentAsync(Transform parent, CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            if (TryTakeIdleView(out var idleView))
            {
                idleView.transform.SetParent(parent, false);
                idleView.gameObject.SetActive(true);
                _rentedViews.Add(idleView);
                return idleView;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);
            var created = await _viewFactory.CreateAsync<TView>(_prefab, parent, linkedCts.Token);

            if (!created.TryPickT0(out var view, out var notFound))
            {
                return notFound;
            }

            if (_isDisposed)
            {
                _viewFactory.Destroy(view);
                throw new OperationCanceledException(linkedCts.Token);
            }

            _rentedViews.Add(view);
            return view;
        }

        public void Return(TView view)
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_rentedViews.Remove(view))
            {
                throw new InvalidOperationException($"View {typeof(TView).Name} was not rented from this pool or was already returned.");
            }

            if (view == null)
            {
                _viewFactory.Destroy(view);
                return;
            }

            view.gameObject.SetActive(false);
            _idleViews.Push(view);
        }

        private bool TryTakeIdleView(out TView view)
        {
            while (_idleViews.Count > 0)
            {
                view = _idleViews.Pop();

                if (view != null)
                {
                    return true;
                }

                _viewFactory.Destroy(view);
            }

            view = null!;
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            foreach (var view in _rentedViews)
            {
                _viewFactory.Destroy(view);
            }

            foreach (var view in _idleViews)
            {
                _viewFactory.Destroy(view);
            }

            _rentedViews.Clear();
            _idleViews.Clear();
        }
    }
}
