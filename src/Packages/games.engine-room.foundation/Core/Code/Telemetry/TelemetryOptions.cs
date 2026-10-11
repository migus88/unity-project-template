using System.Text;
using Core.Environment;
using Core.Logging;

namespace Core.Telemetry
{
    internal sealed record TelemetryOptions(bool IsAllowed, string Endpoint, string IngestKey, string EnvironmentName, string Build, string Platform)
    {
        public static readonly TelemetryOptions None = new(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public static TelemetryOptions From(BuildEnvironment? environment, string build, string platform)
        {
            if (environment == null)
            {
                return None;
            }

            return From(environment.IsTelemetryAllowed, environment.TelemetryEndpoint, environment.TelemetryIngestKey, environment.EnvironmentName, build, platform);
        }

        public static TelemetryOptions From(bool isTelemetryAllowed, string endpoint, string ingestKey, string environmentName, string build, string platform)
        {
            var options = new TelemetryOptions(false, endpoint.Trim(), ingestKey.Trim(), environmentName, build, platform);

            if (!isTelemetryAllowed || options.Endpoint.Length == 0)
            {
                return options;
            }

            var problem = FindProblem(options);

            if (problem != null)
            {
                Log.Error(LogTags.Telemetry, $"Telemetry is off: {problem}");
                return options;
            }

            return options with { IsAllowed = true };
        }

        private static string? FindProblem(TelemetryOptions options)
        {
            if (!TelemetryLimits.BuildPattern.IsMatch(options.Build))
            {
                return $"build version '{options.Build}' does not match {TelemetryLimits.BuildPattern}.";
            }

            if (!HasLength(options.EnvironmentName, TelemetryLimits.MaxEnvironmentBytes))
            {
                return $"environment name '{options.EnvironmentName}' must be 1 to {TelemetryLimits.MaxEnvironmentBytes} bytes.";
            }

            if (!HasLength(options.Platform, TelemetryLimits.MaxPlatformBytes))
            {
                return $"platform '{options.Platform}' must be 1 to {TelemetryLimits.MaxPlatformBytes} bytes.";
            }

            return null;
        }

        private static bool HasLength(string value, int maxBytes)
        {
            var length = Encoding.UTF8.GetByteCount(value);
            return length > 0 && length <= maxBytes;
        }
    }
}
