using Core.Results;
using Core.Save;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Gameplay.Progress
{
    internal static class GameplaySave
    {
        public const string Key = "gameplay";
        public const int CurrentVersion = 2;

        public static readonly SaveSection<GameplaySaveDto> Section = new(Key, CurrentVersion, Migrate);

        public static OneOf<JObject, Corrupted> Migrate(JObject data, int fromVersion)
        {
            var current = data;

            for (var version = fromVersion; version < CurrentVersion; version++)
            {
                OneOf<JObject, Corrupted> migrated = version switch
                {
                    1 => MigrateFromVersion1(current),
                    _ => new Corrupted($"Gameplay save data has no migration from version {version}."),
                };

                if (!migrated.TryPickT0(out current, out var corrupted))
                {
                    return corrupted;
                }
            }

            return current;
        }

        private static OneOf<JObject, Corrupted> MigrateFromVersion1(JObject data)
        {
            if (data["highScore"] is not JValue { Type: JTokenType.Integer } highScore)
            {
                return new Corrupted("Gameplay save data version 1 has no integer 'highScore'.");
            }

            return new JObject
            {
                ["bestScore"] = highScore.Value<int>(),
                ["roundsPlayed"] = 0,
            };
        }
    }
}
