using System;
using System.Text.RegularExpressions;

namespace Core.Telemetry
{
    internal static class TelemetryLimits
    {
        public const int MaxProps = 64;
        public const int MaxStringBytes = 256;
        public const int MaxBatchEvents = 250;
        public const int MaxBatchBytes = 900 * 1024;
        public const int MaxQueuedEvents = 2000;
        public const int MaxEnvironmentBytes = 32;
        public const int MaxPlatformBytes = 32;

        public static readonly Regex NamePattern = new("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant);
        public static readonly Regex BuildPattern = new(@"^\d{1,4}\.\d{1,4}(\.\d{1,4})?([+-][0-9A-Za-z.-]{1,48})?$", RegexOptions.CultureInvariant);
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);
    }
}
