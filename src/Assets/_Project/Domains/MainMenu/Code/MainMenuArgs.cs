namespace MainMenu
{
    public sealed record MainMenuArgs
    {
#if UNITY_EDITOR
        public static MainMenuArgs CreateDebug() => new();
#endif
    }
}
