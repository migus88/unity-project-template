namespace Settings
{
    public sealed record SettingsArgs
    {
#if UNITY_EDITOR
        public static SettingsArgs CreateDebug() => new();
#endif
    }
}
