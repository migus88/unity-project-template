namespace Core.Telemetry
{
    internal sealed record TelemetryEnvelope(string InstallId, string SessionId, string Build, string Platform, string Environment, int PolicyVersion);
}
