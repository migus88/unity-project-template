using System.Threading;
using Bootstrap;
using Core.Domains;
using Core.Logging;
using Cysharp.Threading.Tasks;
using Gameplay;
using MainMenu;

namespace Sample
{
    internal sealed class SampleMainFlow : IMainFlow
    {
        private readonly MainMenuDomain _mainMenu;
        private readonly GameplayDomain _gameplay;

        public SampleMainFlow(MainMenuDomain mainMenu, GameplayDomain gameplay)
        {
            _mainMenu = mainMenu;
            _gameplay = gameplay;
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            while (true)
            {
                var menuResult = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Loading, ct);
                var shouldQuit = menuResult.Match(
                    play => false,
                    quit => true);

                if (shouldQuit)
                {
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
