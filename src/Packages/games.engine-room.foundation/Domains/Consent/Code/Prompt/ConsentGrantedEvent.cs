using Core.Analytics;

namespace Consent.Prompt
{
    internal readonly record struct ConsentGrantedEvent(int PolicyVersion) : IAnalyticsEvent
    {
        public string Name => "consent_granted";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("policy_version", PolicyVersion);
        }
    }
}
