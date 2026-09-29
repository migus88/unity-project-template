using System.Threading;
using Core;
using Core.Domains;
using Core.Logging;
using Cysharp.Threading.Tasks;
using Gameplay;
using MainMenu;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class GameFlow : IAsyncStartable
    {
        private readonly CoreStartup _coreStartup;
        private readonly IApplicationService _application;
        private readonly MainMenuDomain _mainMenu;
        private readonly GameplayDomain _gameplay;

        public GameFlow(CoreStartup coreStartup, IApplicationService application, MainMenuDomain mainMenu, GameplayDomain gameplay)
        {
            _coreStartup = coreStartup;
            _application = application;
            _mainMenu = mainMenu;
            _gameplay = gameplay;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await _coreStartup.RunAsync(ct);

            while (true)
            {
                var menuResult = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Loading, ct);
                var shouldQuit = menuResult.Match(
                    play => false,
                    quit => true);

                if (shouldQuit)
                {
                    Log.Info(LogTags.Flow, "Quitting.");
                    _application.Quit();
                    return;
                }

                var gameplayResult = await _gameplay.RunAsync(new GameplayArgs(LevelIndex: 0), Transition.Loading, ct);
                gameplayResult.Switch(
                    won => Log.Info(LogTags.Flow, $"Won with {won.Score} points in {won.Time.TotalSeconds:0.0} s."),
                    lost => Log.Info(LogTags.Flow, $"Lost with {lost.Score} points."),
                    quitToMenu => Log.Info(LogTags.Flow, "Quit to menu."));
            }
        }
    }
}
