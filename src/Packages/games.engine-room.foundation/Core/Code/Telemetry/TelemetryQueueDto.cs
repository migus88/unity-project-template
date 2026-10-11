namespace Core.Telemetry
{
    internal sealed record TelemetryQueueDto(int? Version, int? DroppedCount, TelemetryQueueGroupDto[]? Groups)
    {
        public const int CurrentVersion = 1;
    }
}
