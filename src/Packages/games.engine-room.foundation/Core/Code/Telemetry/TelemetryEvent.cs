using System.Collections.Generic;

namespace Core.Telemetry
{
    internal sealed record TelemetryEvent(string Name, long Seq, long Time, Dictionary<string, object> Props);
}
