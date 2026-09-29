using Unity.Loading;
using UnityEngine;

namespace Core.Views
{
    public sealed class ViewPoolFactory
    {
        private readonly IViewFactory _viewFactory;

        public ViewPoolFactory(IViewFactory viewFactory)
        {
            _viewFactory = viewFactory;
        }

        public IViewPool<TView> Create<TView>(Loadable<GameObject> prefab) where TView : Component
        {
            return new ViewPool<TView>(_viewFactory, prefab);
        }
    }
}
