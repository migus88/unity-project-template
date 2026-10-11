using Core.Results;
using Core.Settings;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Core.Privacy
{
    internal static class PrivacySettings
    {
        public const int CurrentVersion = 1;
        public const string GrantedValue = "granted";
        public const string DeniedValue = "denied";
        private const string Key = "privacy";

        public static readonly PrivacySettingsDto Default = new(null, null, null);
        public static readonly SettingsSection<PrivacySettingsDto> Section = new(Key, CurrentVersion, Migrate, Default);

        public static OneOf<JObject, Corrupted> Migrate(JObject data, int fromVersion)
        {
            return new Corrupted($"Privacy settings have no migration from version {fromVersion}.");
        }
    }
}
