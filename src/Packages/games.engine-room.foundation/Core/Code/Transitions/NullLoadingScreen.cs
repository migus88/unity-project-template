using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Transitions
{
    public sealed class NullLoadingScreen : ILoadingScreen
    {
        public UniTask ShowAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public UniTask HideAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }
}
