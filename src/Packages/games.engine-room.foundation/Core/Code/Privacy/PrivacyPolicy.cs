using System;
using Core.Environment;
using Core.Logging;

namespace Core.Privacy
{
    public sealed record PrivacyPolicy(string? Url, int Version)
    {
        public static PrivacyPolicy Unpublished { get; } = new(null, 1);

        public static PrivacyPolicy From(BuildEnvironment? environment)
        {
            if (environment == null)
            {
                return Unpublished;
            }

            var version = Math.Max(1, environment.PrivacyPolicyVersion);
            return new PrivacyPolicy(ToPolicyUrl(environment.PrivacyPolicyUrl, environment.name), version);
        }

        private static string? ToPolicyUrl(string url, string environmentName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                return url;
            }

            Log.Warn(LogTags.Privacy, $"Build environment '{environmentName}' has a privacy policy URL that is not an absolute https URL; the policy link is hidden.");
            return null;
        }
    }
}
