namespace Core.Analytics
{
    public interface IAnalyticsBackend
    {
        void Send<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent;
    }
}
