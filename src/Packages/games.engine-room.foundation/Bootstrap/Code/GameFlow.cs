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
        private readonly LoadingScreen _loadingScreen;
        private readonly LoadingDomain _loading;
        private readonly IMainFlow _mainFlow;
        private readonly BootCoverView _bootCover;

        public GameFlow(CoreStartup coreStartup, IApplicationService application, LoadingScreen loadingScreen, LoadingDomain loading, IMainFlow mainFlow, BootCoverView bootCover)
        {
            _coreStartup = coreStartup;
            _application = application;
            _loadingScreen = loadingScreen;
            _loading = loading;
            _mainFlow = mainFlow;
            _bootCover = bootCover;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var loadingRun = RunLoadingAsync(ct).Preserve();
            await _loadingScreen.ShowAsync(ct);
            await UniTask.WhenAny(_loadingScreen.WaitForViewAsync(ct), loadingRun);
            _bootCover.Hide();
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
