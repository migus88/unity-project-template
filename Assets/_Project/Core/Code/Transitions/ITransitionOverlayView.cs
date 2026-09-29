using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Transitions
{
    public interface ITransitionOverlayView
    {
        UniTask FadeInAsync(CancellationToken ct);
        UniTask FadeOutAsync(CancellationToken ct);
    }
}
