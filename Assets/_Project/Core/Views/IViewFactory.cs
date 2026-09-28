using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine;

namespace Core.Views
{
    public interface IViewFactory
    {
        UniTask<OneOf<TView, NotFound>> CreateAsync<TView>(Loadable<GameObject> prefab, Transform parent, CancellationToken ct)
            where TView : Component;

        void Destroy<TView>(TView view) where TView : Component;
    }
}
