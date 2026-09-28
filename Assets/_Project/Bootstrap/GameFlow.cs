using System.Threading;
using Core;
using Core.Domains;
using Core.Logging;
using Cysharp.Threading.Tasks;
using MainMenu;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class GameFlow : IAsyncStartable
    {
        private readonly CoreStartup _coreStartup;
        private readonly IApplicationService _application;
        private readonly MainMenuDomain _mainMenu;

        public GameFlow(CoreStartup coreStartup, IApplicationService application, MainMenuDomain mainMenu)
        {
            _coreStartup = coreStartup;
            _application = application;
            _mainMenu = mainMenu;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await _coreStartup.RunAsync(ct);

            while (true)
            {
                var menuResult = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Fade, ct);
                var shouldQuit = menuResult.Match(
                    play => false,
                    quit => true);

                if (shouldQuit)
                {
                    Log.Info(LogTags.Flow, "Quitting.");
                    _application.Quit();
                    return;
                }

                await PlayAsync(ct);
            }
        }

        private UniTask PlayAsync(CancellationToken ct)
        {
            Log.Info(LogTags.Flow, "Gameplay is not available yet, returning to the main menu.");
            return UniTask.CompletedTask;
        }
    }
}
