using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Unity.Loading;
using UnityEngine.SceneManagement;

namespace Core.Content
{
    public interface ISceneLoader
    {
        UniTask<OneOf<Scene, NotFound>> LoadAdditiveAsync(LoadableSceneId id, CancellationToken ct);
        UniTask UnloadAsync(Scene scene, CancellationToken ct);
    }
}
