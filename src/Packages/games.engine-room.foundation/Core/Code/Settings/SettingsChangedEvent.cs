using Core.Analytics;

namespace Core.Settings
{
    public readonly record struct SettingsChangedEvent(string Setting, string Value) : IAnalyticsEvent
    {
        public string Name => "settings_changed";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("setting", Setting);
            writer.Add("value", Value);
        }
    }
}
