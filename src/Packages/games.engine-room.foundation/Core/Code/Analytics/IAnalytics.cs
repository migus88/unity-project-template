namespace Core.Analytics
{
    public interface IAnalytics
    {
        void Track<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent;
    }
}
