using System.Threading;
using Core.Logging;
using Core.Transitions;
using Cysharp.Threading.Tasks;

namespace Bootstrap
{
    internal sealed class IdleMainFlow : IMainFlow
    {
        private readonly ILoadingScreen _loadingScreen;

        public IdleMainFlow(ILoadingScreen loadingScreen)
        {
            _loadingScreen = loadingScreen;
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            Log.Warn(LogTags.Flow, "The root scope has no game module, so there is no main flow to run.");
            await _loadingScreen.HideAsync(ct);
            await UniTask.Never(ct);
        }
    }
}
