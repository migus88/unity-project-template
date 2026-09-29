using Core.Results;
using Core.Settings;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Gameplay.UserSettings
{
    internal static class GameplaySettings
    {
        public const int CurrentVersion = 1;
        private const string Key = "gameplay";

        public static readonly GameplaySettingsDto Default = new(CameraDistance: 0.5f);
        public static readonly SettingsSection<GameplaySettingsDto> Section = new(Key, CurrentVersion, Migrate, Default);

        public static OneOf<JObject, Corrupted> Migrate(JObject data, int fromVersion)
        {
            return new Corrupted($"Gameplay settings have no migration from version {fromVersion}.");
        }
    }
}
