using System.Threading;
using Cysharp.Threading.Tasks;

namespace Loading
{
    internal interface ILoadingScreenView
    {
        void SetVisible(bool isVisible);
        UniTask FadeAsync(bool isVisible, CancellationToken ct);
    }
}
