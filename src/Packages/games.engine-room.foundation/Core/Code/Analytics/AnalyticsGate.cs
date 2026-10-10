namespace Core.Analytics
{
    public static class AnalyticsGate
    {
#if ANALYTICS_ENABLED
        public const bool IsEnabled = true;
#else
        public const bool IsEnabled = false;
#endif

        public static IAnalytics Create(bool isEnabled, IAnalyticsBackend backend)
        {
            return isEnabled ? new AnalyticsService(backend) : new NullAnalytics();
        }
    }
}
