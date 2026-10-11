using Core.Analytics;

namespace Settings.Privacy
{
    internal readonly record struct ConsentChangedEvent(bool IsGranted) : IAnalyticsEvent
    {
        public string Name => "consent_changed";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("granted", IsGranted);
            writer.Add("source", "settings");
        }
    }
}
