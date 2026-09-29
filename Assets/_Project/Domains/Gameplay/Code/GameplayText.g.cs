using Core.Localization;

namespace Gameplay
{
    internal static class GameplayText
    {
        public static readonly TextKey Score = new("Gameplay", "score");
        public static readonly TextKey TimeLeft = new("Gameplay", "time_left");
        public static readonly TextKey BestScore = new("Gameplay", "best_score");
        public static readonly TextKey NewBestScore = new("Gameplay", "new_best_score");
        public static readonly TextKey YouWon = new("Gameplay", "you_won");
        public static readonly TextKey YouLost = new("Gameplay", "you_lost");
        public static readonly TextKey Continue = new("Gameplay", "continue");
        public static readonly TextKey Paused = new("Gameplay", "paused");
        public static readonly TextKey Resume = new("Gameplay", "resume");
        public static readonly TextKey QuitToMenu = new("Gameplay", "quit_to_menu");
    }
}
