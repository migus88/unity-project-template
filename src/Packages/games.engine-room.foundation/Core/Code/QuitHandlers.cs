using System;
using System.Collections.Generic;
using System.Threading;
using Core.Logging;
using Cysharp.Threading.Tasks;
using R3;

namespace Core
{
    internal sealed class QuitHandlers
    {
        private readonly List<Func<CancellationToken, UniTask>> _handlers = new();

        public IDisposable Add(Func<CancellationToken, UniTask> handler)
        {
            _handlers.Add(handler);
            return Disposable.Create(() => _handlers.Remove(handler));
        }

        public async UniTask RunAsync(TimeSpan budget, CancellationToken ct)
        {
            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timer = budgetCts.CancelAfterSlim(budget, DelayType.Realtime);
            var runs = new UniTask[_handlers.Count];

            for (var i = 0; i < runs.Length; i++)
            {
                runs[i] = RunHandlerAsync(_handlers[i], budgetCts.Token);
            }

            await UniTask.WhenAll(runs);
        }

        private static async UniTask RunHandlerAsync(Func<CancellationToken, UniTask> handler, CancellationToken ct)
        {
            try
            {
                await handler(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Log.Warn(LogTags.Application, "A quit handler ran out of time and was cancelled.");
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
            }
        }
    }
}
