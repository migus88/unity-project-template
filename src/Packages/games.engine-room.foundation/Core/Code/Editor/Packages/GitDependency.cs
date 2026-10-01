using System;
using Newtonsoft.Json.Linq;

namespace Core.Editor.Packages
{
    internal static class GitDependency
    {
        private static readonly string[] GitPrefixes = { "git+", "git@", "git:", "ssh:" };
        private static readonly string[] HttpPrefixes = { "http://", "https://" };

        public static string? FindEntry(string manifestJson, string packageName)
        {
            var dependencies = JObject.Parse(manifestJson)["dependencies"] as JObject;
            return dependencies?[packageName]?.Value<string>();
        }

        public static bool IsGitUrl(string? entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return false;
            }

            var value = entry.Trim();

            if (StartsWithAny(value, GitPrefixes))
            {
                return true;
            }

            if (!StartsWithAny(value, HttpPrefixes))
            {
                return false;
            }

            var end = value.IndexOfAny(new[] { '?', '#' });
            var address = end < 0 ? value : value.Substring(0, end);
            return address.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
        }

        public static string? GetRevision(string url)
        {
            var index = url.LastIndexOf('#');

            if (index < 0 || index == url.Length - 1)
            {
                return null;
            }

            var revision = url.Substring(index + 1);
            var queryIndex = revision.IndexOf('?');
            return queryIndex < 0 ? revision : revision.Substring(0, queryIndex);
        }

        public static bool IsSameVersion(string? revision, string version)
        {
            return revision != null
                && (string.Equals(revision, version, StringComparison.Ordinal)
                    || string.Equals(revision, "v" + version, StringComparison.OrdinalIgnoreCase));
        }

        private static bool StartsWithAny(string value, string[] prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
