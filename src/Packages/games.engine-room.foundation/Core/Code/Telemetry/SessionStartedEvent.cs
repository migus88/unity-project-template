using Core.Analytics;

namespace Core.Telemetry
{
    internal readonly record struct SessionStartedEvent : IAnalyticsEvent
    {
        public string Name => "session_started";

        public void Write(IAnalyticsWriter writer)
        {
        }
    }
}
