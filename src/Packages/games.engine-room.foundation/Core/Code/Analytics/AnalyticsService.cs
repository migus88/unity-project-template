namespace Core.Analytics
{
    internal sealed class AnalyticsService : IAnalytics
    {
        private readonly IAnalyticsBackend _backend;

        public AnalyticsService(IAnalyticsBackend backend)
        {
            _backend = backend;
        }

        public void Track<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
            _backend.Send(analyticsEvent);
        }
    }
}
