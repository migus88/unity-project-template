namespace Core.Platform
{
    public interface IAchievements
    {
        void Unlock(Achievement achievement);

        void ReportProgress(Achievement achievement, int current);

        AchievementStatus GetStatus(Achievement achievement);
    }
}
