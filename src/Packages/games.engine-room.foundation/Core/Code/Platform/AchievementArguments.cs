using System;

namespace Core.Platform
{
    public static class AchievementArguments
    {
        public static void ValidateProgress(Achievement achievement, int current)
        {
            if (achievement.IsUnlockOnly)
            {
                throw new ArgumentException($"Achievement '{achievement.Id}' is unlock-only and takes no progress.", nameof(achievement));
            }

            if (current < 0)
            {
                throw new ArgumentException($"Progress {current} for achievement '{achievement.Id}' is negative.", nameof(current));
            }
        }
    }
}
