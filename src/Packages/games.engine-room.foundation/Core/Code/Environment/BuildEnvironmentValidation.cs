using System;
using System.Collections.Generic;

namespace Core.Environment
{
    public static class BuildEnvironmentValidation
    {
        public static IReadOnlyList<string> Validate(BuildEnvironment environment)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(environment.EnvironmentName))
            {
                problems.Add("the environment name is empty.");
            }

            if (environment.IsTelemetryAllowed && !IsHttpsUrl(environment.TelemetryEndpoint))
            {
                problems.Add($"telemetry is allowed but the telemetry endpoint '{environment.TelemetryEndpoint}' is not an absolute https URL.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in environment.RemoteConfigDefaults)
            {
                if (!keys.Add(entry.Key ?? string.Empty))
                {
                    problems.Add($"the remote-config key '{entry.Key}' is repeated.");
                }
            }

            return problems;
        }

        private static bool IsHttpsUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
