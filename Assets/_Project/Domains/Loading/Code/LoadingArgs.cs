namespace Loading
{
    public sealed record LoadingArgs
    {
#if UNITY_EDITOR
        public static LoadingArgs CreateDebug() => new();
#endif
    }
}
