#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Core.Cheats
{
    internal sealed class CheatCompleter
    {
        private readonly CheatRegistry _registry;

        public CheatCompleter(CheatRegistry registry)
        {
            _registry = registry;
        }

        public IReadOnlyList<string> Suggest(string line)
        {
            return FindTarget(CheatLine.Parse(line)).Candidates;
        }

        public CheatCompletion Complete(string text, int cycleIndex)
        {
            var line = CheatLine.Parse(text);
            var target = FindTarget(line);
            var candidates = target.Candidates;

            if (candidates.Count == 0)
            {
                return new CheatCompletion(text, false);
            }

            var head = line.Text.Substring(0, target.Start);

            if (candidates.Count == 1)
            {
                return new CheatCompletion(string.Concat(head, Format(candidates[0], target), " "), false);
            }

            var common = CommonPrefix(candidates);

            if (CheatValues.Normalize(common).Length > CheatValues.Normalize(target.Partial).Length)
            {
                return new CheatCompletion(string.Concat(head, Format(common, target)), false);
            }

            var index = ((cycleIndex % candidates.Count) + candidates.Count) % candidates.Count;
            return new CheatCompletion(string.Concat(head, Format(candidates[index], target)), true);
        }

        private Target FindTarget(CheatLine line)
        {
            if (line.IsEmpty)
            {
                return Target.None;
            }

            if (line.Tokens.Count == 1 && !line.EndsWithSpace)
            {
                return CommandTarget(line.Tokens[0]);
            }

            var cheat = _registry.Find(line.Name);

            if (cheat == null || cheat.Parameters.Count == 0)
            {
                return Target.None;
            }

            var parameters = cheat.Parameters;
            var argumentIndex = (line.EndsWithSpace ? line.Tokens.Count : line.Tokens.Count - 1) - 1;
            var lastIndex = parameters.Count - 1;

            if (argumentIndex >= lastIndex && CheatArgumentParser.IsRestOfLine(parameters, lastIndex))
            {
                var firstToken = lastIndex + 1;
                var start = firstToken < line.Tokens.Count ? line.Tokens[firstToken].Start : line.Text.Length;
                return ValueTarget(parameters[lastIndex], start, line.JoinValues(firstToken), true);
            }

            if (argumentIndex > lastIndex)
            {
                return Target.None;
            }

            return line.EndsWithSpace
                ? ValueTarget(parameters[argumentIndex], line.Text.Length, string.Empty, false)
                : ValueTarget(parameters[argumentIndex], line.Tokens[^1].Start, line.Tokens[^1].Value, false);
        }

        private Target CommandTarget(CheatToken token)
        {
            var partial = token.Value.ToLowerInvariant();
            var names = new List<string>();

            foreach (var cheat in _registry.Cheats)
            {
                if (cheat.Name.StartsWith(partial, StringComparison.Ordinal))
                {
                    names.Add(cheat.Name);
                }
            }

            return new Target(token.Start, token.Value, names, true);
        }

        private static Target ValueTarget(CheatParameter parameter, int start, string partial, bool isRestOfLine)
        {
            var values = new List<string>();

            foreach (var value in parameter.Values())
            {
                if (CheatValues.IsPrefix(value, partial))
                {
                    values.Add(value);
                }
            }

            return new Target(start, partial, values, isRestOfLine);
        }

        private static string Format(string value, Target target)
        {
            return target.IsRestOfLine || value.IndexOf(' ') < 0 ? value : string.Concat(CheatLine.Quote.ToString(), value, CheatLine.Quote.ToString());
        }

        private static string CommonPrefix(IReadOnlyList<string> values)
        {
            var first = values[0];
            var length = first.Length;

            for (var i = 1; i < values.Count; i++)
            {
                var other = values[i];
                var shared = 0;

                while (shared < length && shared < other.Length && char.ToLowerInvariant(first[shared]) == char.ToLowerInvariant(other[shared]))
                {
                    shared++;
                }

                length = shared;
            }

            return first.Substring(0, length);
        }

        private readonly record struct Target(int Start, string Partial, IReadOnlyList<string> Candidates, bool IsRestOfLine)
        {
            public static readonly Target None = new(0, string.Empty, Array.Empty<string>(), false);
        }
    }
}
#endif
