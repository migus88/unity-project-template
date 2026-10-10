namespace Core.Analytics
{
    public interface IAnalyticsEvent
    {
        string Name { get; }

        void Write(IAnalyticsWriter writer);
    }
}
