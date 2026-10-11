using System;

namespace Core.RemoteConfig
{
    internal static class RemoteConfigUrl
    {
        private const string ConfigPath = "/v1/config";

        public static string Build(string endpoint, string buildVersion, string platform)
        {
            return $"{endpoint.TrimEnd('/')}{ConfigPath}?build={Uri.EscapeDataString(buildVersion)}&platform={Uri.EscapeDataString(platform)}";
        }
    }
}
