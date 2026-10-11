using System;
using Core.Analytics;

namespace Core.Tests.Telemetry
{
    internal readonly record struct TestTelemetryEvent(string Name, Action<IAnalyticsWriter>? Props = null) : IAnalyticsEvent
    {
        public void Write(IAnalyticsWriter writer)
        {
            Props?.Invoke(writer);
        }
    }
}
