using Core.Analytics;

namespace Settings.Analytics
{
    internal readonly record struct SettingsOpenedEvent(string From) : IAnalyticsEvent
    {
        public string Name => "settings_opened";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("from", From);
        }
    }
}
