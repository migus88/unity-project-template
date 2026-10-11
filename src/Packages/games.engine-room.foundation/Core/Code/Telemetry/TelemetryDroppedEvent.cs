using Core.Analytics;

namespace Core.Telemetry
{
    internal readonly record struct TelemetryDroppedEvent(int Count) : IAnalyticsEvent
    {
        public string Name => "telemetry_dropped";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("count", Count);
        }
    }
}
