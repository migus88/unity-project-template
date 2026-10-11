using System;
using System.Collections.Generic;

namespace Core.Platform
{
    public sealed class AchievementIdMap
    {
        private readonly AchievementCatalog _catalog;
        private readonly Dictionary<string, string> _platformIds;
        private readonly Dictionary<string, Achievement> _achievementsByPlatformId;

        public AchievementIdMap(AchievementCatalog catalog, IReadOnlyDictionary<string, string> platformIdOverrides)
        {
            var problems = FindProblems(catalog, platformIdOverrides);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"Invalid achievement platform ids: {string.Join("; ", problems)}");
            }

            _catalog = catalog;
            _platformIds = new Dictionary<string, string>(catalog.All.Count, StringComparer.Ordinal);
            _achievementsByPlatformId = new Dictionary<string, Achievement>(catalog.All.Count, StringComparer.Ordinal);

            foreach (var achievement in catalog.All)
            {
                var platformId = ResolvePlatformId(achievement, platformIdOverrides);
                _platformIds.Add(achievement.Id, platformId);
                _achievementsByPlatformId.Add(platformId, achievement);
            }
        }

        public static IReadOnlyList<string> FindProblems(AchievementCatalog catalog, IReadOnlyDictionary<string, string> platformIdOverrides)
        {
            var problems = new List<string>();

            foreach (var pair in platformIdOverrides)
            {
                if (!catalog.TryGet(pair.Key, out _))
                {
                    problems.Add($"override for unknown achievement '{pair.Key}'");
                }

                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    problems.Add($"blank platform id for '{pair.Key}'");
                }
            }

            var owners = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var achievement in catalog.All)
            {
                var platformId = ResolvePlatformId(achievement, platformIdOverrides);

                if (string.IsNullOrWhiteSpace(platformId))
                {
                    continue;
                }

                if (owners.TryGetValue(platformId, out var owner))
                {
                    problems.Add($"'{owner}' and '{achievement.Id}' share platform id '{platformId}'");
                }
                else
                {
                    owners.Add(platformId, achievement.Id);
                }
            }

            return problems;
        }

        public string ToPlatformId(Achievement achievement)
        {
            if (!_catalog.Contains(achievement))
            {
                throw new ArgumentException($"Achievement '{achievement.Id}' is not in the achievement catalog.", nameof(achievement));
            }

            return _platformIds[achievement.Id];
        }

        public bool TryGetAchievement(string platformId, out Achievement achievement)
        {
            return _achievementsByPlatformId.TryGetValue(platformId, out achievement!);
        }

        private static string ResolvePlatformId(Achievement achievement, IReadOnlyDictionary<string, string> platformIdOverrides)
        {
            return platformIdOverrides.TryGetValue(achievement.Id, out var platformId) ? platformId : achievement.Id;
        }
    }
}
