using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;

namespace Core.Content
{
    public interface IContentLoader
    {
        UniTask<OneOf<T, NotFound>> LoadAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object;
        void Release<T>(Loadable<T> loadable) where T : UnityEngine.Object;
    }
}
