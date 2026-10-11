namespace Core.Telemetry
{
    internal sealed record TelemetryQueueGroupDto(TelemetryEnvelope? Envelope, TelemetryEvent[]? Events);
}
