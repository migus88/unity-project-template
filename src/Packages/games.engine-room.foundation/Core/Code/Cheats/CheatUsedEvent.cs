#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.Analytics;

namespace Core.Cheats
{
    public readonly record struct CheatUsedEvent(string Command) : IAnalyticsEvent
    {
        public string Name => "cheat_used";

        public void Write(IAnalyticsWriter writer)
        {
            writer.Add("command", Command);
        }
    }
}
#endif
