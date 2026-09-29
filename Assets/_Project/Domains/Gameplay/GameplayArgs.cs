namespace Gameplay
{
    public sealed record GameplayArgs(int LevelIndex)
    {
#if UNITY_EDITOR
        public static GameplayArgs CreateDebug() => new(LevelIndex: 0);
#endif
    }
}
