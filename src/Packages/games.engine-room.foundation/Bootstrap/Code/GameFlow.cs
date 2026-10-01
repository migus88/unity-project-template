using System;
using System.Threading;
using Core;
using Core.Domains;
using Core.Logging;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using Loading;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class GameFlow : IAsyncStartable
    {
        private readonly CoreStartup _coreStartup;
        private readonly IApplicationService _application;
        private readonly ILoadingScreen _loadingScreen;
        private readonly LoadingDomain _loading;
        private readonly IMainFlow _mainFlow;

        public GameFlow(CoreStartup coreStartup, IApplicationService application, ILoadingScreen loadingScreen, LoadingDomain loading, IMainFlow mainFlow)
        {
            _coreStartup = coreStartup;
            _application = application;
            _loadingScreen = loadingScreen;
            _loading = loading;
            _mainFlow = mainFlow;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var loadingRun = RunLoadingAsync(ct);
            await _loadingScreen.ShowAsync(ct);
            await _coreStartup.RunAsync(ct);
            await _mainFlow.RunAsync(ct);

            Log.Info(LogTags.Flow, "Quitting.");
            _application.Quit();
            await loadingRun;
        }

        private async UniTask RunLoadingAsync(CancellationToken ct)
        {
            try
            {
                await _loading.RunAsync(new LoadingArgs(), Transition.None, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Exception(exception);
            }
        }
    }
}
