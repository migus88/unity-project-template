using System;
using OneOf;

namespace Gameplay
{
    [GenerateOneOf]
    public partial class GameplayResult : OneOfBase<GameplayResult.Won, GameplayResult.Lost, GameplayResult.QuitToMenu>
    {
        public readonly record struct Won(int Score, TimeSpan Time);
        public readonly record struct Lost(int Score);
        public readonly record struct QuitToMenu;
    }
}
