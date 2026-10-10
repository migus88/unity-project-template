using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Core.Settings
{
    public static class SettingsChanges
    {
        public const string BindingsValue = "changed";

        public static IReadOnlyList<SettingsChangedEvent> Diff(SettingsState before, SettingsState after)
        {
            var changes = new List<SettingsChangedEvent>();
            AddVolume(changes, "master_volume", before.MasterVolume, after.MasterVolume);
            AddVolume(changes, "music_volume", before.MusicVolume, after.MusicVolume);
            AddVolume(changes, "sfx_volume", before.SfxVolume, after.SfxVolume);
            AddVolume(changes, "ui_volume", before.UiVolume, after.UiVolume);
            Add(changes, before.Language != after.Language, "language", after.Language.ToString());
            Add(changes, before.QualityLevel != after.QualityLevel, "quality", after.QualityLevel.ToString(CultureInfo.InvariantCulture));
            Add(changes, before.FullScreenMode != after.FullScreenMode, "fullscreen", after.FullScreenMode.ToString());
            Add(changes, !IsSameResolution(before.Resolution, after.Resolution), "resolution", FormatResolution(after.Resolution));
            Add(changes, before.VSync != after.VSync, "vsync", after.VSync ? "true" : "false");
            Add(changes, before.BindingOverridesJson != after.BindingOverridesJson, "bindings", BindingsValue);
            return changes;
        }

        private static void AddVolume(List<SettingsChangedEvent> changes, string setting, float before, float after)
        {
            var formatted = FormatVolume(after);
            Add(changes, FormatVolume(before) != formatted, setting, formatted);
        }

        private static void Add(List<SettingsChangedEvent> changes, bool isChanged, string setting, string value)
        {
            if (isChanged)
            {
                changes.Add(new SettingsChangedEvent(setting, value));
            }
        }

        private static string FormatVolume(float volume)
        {
            return volume.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool IsSameResolution(Resolution before, Resolution after)
        {
            return before.width == after.width && before.height == after.height && before.refreshRateRatio.Equals(after.refreshRateRatio);
        }

        private static string FormatResolution(Resolution resolution)
        {
            return $"{resolution.width.ToString(CultureInfo.InvariantCulture)}x{resolution.height.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
