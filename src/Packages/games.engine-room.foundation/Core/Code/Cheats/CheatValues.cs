#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Core.Cheats
{
    public static class CheatValues
    {
        public static string? Match(IEnumerable<string> values, string input)
        {
            return Match(values, value => value, input);
        }

        public static T? Match<T>(IEnumerable<T> items, Func<T, string> nameOf, string input) where T : class
        {
            var matches = Matches(items, nameOf, input);
            return matches.Count == 1 ? matches[0] : null;
        }

        public static IReadOnlyList<T> Matches<T>(IEnumerable<T> items, Func<T, string> nameOf, string input) where T : class
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var wanted = Normalize(input);
            var prefixMatches = new List<T>();

            if (wanted.Length == 0)
            {
                return prefixMatches;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var name = Normalize(nameOf(item));

                if (name == wanted)
                {
                    return new[] { item };
                }

                if (name.StartsWith(wanted, StringComparison.Ordinal))
                {
                    prefixMatches.Add(item);
                }
            }

            return prefixMatches;
        }

        public static bool IsPrefix(string value, string input)
        {
            return Normalize(value).StartsWith(Normalize(input), StringComparison.Ordinal);
        }

        public static string Normalize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }
    }
}
#endif
