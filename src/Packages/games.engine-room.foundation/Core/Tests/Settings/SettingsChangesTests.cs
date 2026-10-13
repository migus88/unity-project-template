using AwesomeAssertions;
using Core.Localization;
using Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Core.Tests.Settings
{
    public sealed class SettingsChangesTests
    {
        private static readonly SettingsState Before = new(1f, 0.8f, 0.8f, 0.8f, Language.English, 2, FullScreenMode.Windowed, new Resolution { width = 1920, height = 1080 }, true, "{}");

        [Test]
        public void Diff_Unchanged_ReturnsNothing()
        {
            // Act
            var changes = SettingsChanges.Diff(Before, Before with { });

            // Assert
            changes.Should().BeEmpty();
        }

        [Test]
        public void Diff_VolumeAndQuality_ReturnsExactlyThoseFields()
        {
            // Act
            var changes = SettingsChanges.Diff(Before, Before with { MusicVolume = 0.456f, QualityLevel = 1 });

            // Assert
            changes.Should().Equal(new SettingsChangedEvent("music_volume", "0.46"), new SettingsChangedEvent("quality", "1"));
        }

        [Test]
        public void Diff_Bindings_ReportsChangedWithoutTheJson()
        {
            // Act
            var changes = SettingsChanges.Diff(Before, Before with { BindingOverridesJson = "{\"jump\":\"<Keyboard>/k\"}" });

            // Assert
            changes.Should().Equal(new SettingsChangedEvent("bindings", SettingsChanges.BindingsValue));
        }
    }
}
