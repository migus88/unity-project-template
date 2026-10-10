using System.Collections.Generic;
using Core.Analytics;

namespace TestUtils
{
    public sealed class FakeAnalytics : IAnalytics
    {
        public IReadOnlyList<IAnalyticsEvent> Tracked => _tracked;

        private readonly List<IAnalyticsEvent> _tracked = new();

        public void Track<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
            _tracked.Add(analyticsEvent);
        }

        public IReadOnlyList<TEvent> OfType<TEvent>() where TEvent : struct, IAnalyticsEvent
        {
            var matching = new List<TEvent>();

            foreach (var tracked in _tracked)
            {
                if (tracked is TEvent analyticsEvent)
                {
                    matching.Add(analyticsEvent);
                }
            }

            return matching;
        }
    }
}
