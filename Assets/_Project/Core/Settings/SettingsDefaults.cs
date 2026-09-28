using Core.Localization;

namespace Core.Settings
{
    public sealed record SettingsDefaults(float MasterVolume, float MusicVolume, float SfxVolume, float UiVolume, Language Language);
}
