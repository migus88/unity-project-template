#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.Logging;
#endif

namespace Core.Analytics
{
    public sealed class DummyAnalyticsBackend : IAnalyticsBackend
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly AnalyticsLine _line = new();
#endif

        public void Send<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Log.Info(LogTags.Analytics, _line.Format(analyticsEvent));
#endif
        }
    }
}
