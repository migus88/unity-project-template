using OneOf;

namespace MainMenu
{
    [GenerateOneOf]
    public partial class MainMenuResult : OneOfBase<MainMenuResult.Play, MainMenuResult.Quit>
    {
        public readonly record struct Play;
        public readonly record struct Quit;
    }
}
