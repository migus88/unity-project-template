namespace Settings
{
    public sealed record SettingsArgs
    {
        public string From { get; init; } = "unknown";

#if UNITY_EDITOR
        public static SettingsArgs CreateDebug() => new();
#endif
    }
}
