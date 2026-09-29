using OneOf;

namespace Gameplay.Pause
{
    [GenerateOneOf]
    internal sealed partial class PauseResult : OneOfBase<PauseResult.Resume, PauseResult.OpenSettings, PauseResult.QuitToMenu>
    {
        public readonly record struct Resume;
        public readonly record struct OpenSettings;
        public readonly record struct QuitToMenu;
    }
}
