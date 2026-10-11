using System.Collections.Generic;

namespace Core.Telemetry
{
    internal sealed record TelemetryBatch(TelemetryEnvelope Envelope, IReadOnlyList<TelemetryEvent> Events);
}
