namespace Core.Platform
{
    public sealed class NullAchievements : IAchievements
    {
        public void Unlock(Achievement achievement)
        {
        }

        public void ReportProgress(Achievement achievement, int current)
        {
            AchievementArguments.ValidateProgress(achievement, current);
        }

        public AchievementStatus GetStatus(Achievement achievement)
        {
            return new AchievementStatus.Unavailable();
        }
    }
}
