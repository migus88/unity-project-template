using System;
using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using UnityEngine;

namespace Core.Views
{
    public interface IViewPool<TView> : IDisposable where TView : Component
    {
        UniTask<OneOf<TView, NotFound>> RentAsync(Transform parent, CancellationToken ct);
        void Return(TView view);
    }
}
