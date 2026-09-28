using System.Threading;
using Core.Logging;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class GameFlow : IAsyncStartable
    {
        public UniTask StartAsync(CancellationToken ct)
        {
            Log.Info(LogTags.Flow, "Started.");
            return UniTask.CompletedTask;
        }
    }
}
