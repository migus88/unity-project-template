using System.Threading;
using Core;
using Core.Logging;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class GameFlow : IAsyncStartable
    {
        private readonly CoreStartup _coreStartup;

        public GameFlow(CoreStartup coreStartup)
        {
            _coreStartup = coreStartup;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await _coreStartup.RunAsync(ct);
            Log.Info(LogTags.Flow, "Started.");
        }
    }
}
