using Core.Localization;
using UnityEngine;

namespace Core.Settings
{
    public sealed record SettingsState(float MasterVolume, float MusicVolume, float SfxVolume, float UiVolume, Language Language, int QualityLevel, FullScreenMode FullScreenMode, Resolution Resolution, bool VSync, string BindingOverridesJson);
}
