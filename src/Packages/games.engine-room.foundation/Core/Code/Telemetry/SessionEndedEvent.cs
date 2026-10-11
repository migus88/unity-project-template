using Core.Analytics;

namespace Core.Telemetry
{
    internal readonly record struct SessionEndedEvent(int DurationSeconds) : IAnalyticsEvent
    {
        public string Name => "session_ended";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("duration_s", DurationSeconds);
        }
    }
}
