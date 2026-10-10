namespace Core.Analytics
{
    public sealed class NullAnalytics : IAnalytics
    {
        public void Track<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
        }
    }
}
