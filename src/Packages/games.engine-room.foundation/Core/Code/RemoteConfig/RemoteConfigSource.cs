using System;
using System.Collections.Generic;

namespace Core.RemoteConfig
{
    public sealed record RemoteConfigSource(string? Endpoint, string EnvironmentName, IReadOnlyList<KeyValuePair<string, string>> Defaults, string BuildVersion, string Platform)
    {
        public const string NoEnvironmentName = "none";

        public static RemoteConfigSource None(string buildVersion, string platform)
        {
            return new RemoteConfigSource(null, NoEnvironmentName, Array.Empty<KeyValuePair<string, string>>(), buildVersion, platform);
        }
    }
}
