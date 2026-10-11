using System;
using System.Collections.Generic;

namespace Core.Platform
{
    public sealed class AchievementCatalog
    {
        public static AchievementCatalog Empty { get; } = new(Array.Empty<Achievement>());

        public IReadOnlyList<Achievement> All { get; }

        private readonly Dictionary<string, Achievement> _byId;

        public AchievementCatalog(IReadOnlyList<Achievement> achievements)
        {
            _byId = new Dictionary<string, Achievement>(achievements.Count, StringComparer.Ordinal);

            foreach (var achievement in achievements)
            {
                if (!_byId.TryAdd(achievement.Id, achievement))
                {
                    throw new InvalidOperationException($"The achievement catalog declares '{achievement.Id}' twice.");
                }
            }

            All = achievements;
        }

        public bool TryGet(string id, out Achievement achievement)
        {
            return _byId.TryGetValue(id, out achievement!);
        }

        public bool Contains(Achievement achievement)
        {
            return _byId.TryGetValue(achievement.Id, out var declared) && ReferenceEquals(declared, achievement);
        }
    }
}
