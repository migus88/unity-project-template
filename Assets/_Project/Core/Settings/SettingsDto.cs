namespace Core.Settings
{
    internal sealed record SettingsDto(
        int FormatVersion,
        float? MasterVolume,
        float? MusicVolume,
        float? SfxVolume,
        float? UiVolume,
        string? Language,
        int? QualityLevel,
        string? FullScreenMode,
        int? ResolutionWidth,
        int? ResolutionHeight,
        uint? RefreshRateNumerator,
        uint? RefreshRateDenominator,
        bool? VSync,
        string? BindingOverridesJson);
}
