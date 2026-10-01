using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Transitions
{
    public interface ILoadingScreen
    {
        UniTask ShowAsync(CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }
}
