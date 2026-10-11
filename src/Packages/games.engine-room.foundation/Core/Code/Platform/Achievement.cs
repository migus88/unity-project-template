using System;
using System.Text.RegularExpressions;

namespace Core.Platform
{
    public sealed class Achievement
    {
        private static readonly Regex IdPattern = new("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant);

        public string Id { get; }

        public int ProgressTarget { get; }

        public bool IsUnlockOnly => ProgressTarget == 0;

        public Achievement(string id, int progressTarget = 0)
        {
            if (id == null || !IdPattern.IsMatch(id))
            {
                throw new ArgumentException($"Achievement id '{id}' must be snake_case: a lowercase letter, then up to 63 lowercase letters, digits or underscores.", nameof(id));
            }

            if (progressTarget < 0)
            {
                throw new ArgumentException($"Achievement '{id}' has a negative progress target {progressTarget}.", nameof(progressTarget));
            }

            Id = id;
            ProgressTarget = progressTarget;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
