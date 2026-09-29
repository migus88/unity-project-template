using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Core.Transitions
{
    public interface ISceneTransitionService
    {
        UniTask ShowAsync(Transition transition, CancellationToken ct);
        UniTask HideAsync(Transition transition, CancellationToken ct);
    }
}
