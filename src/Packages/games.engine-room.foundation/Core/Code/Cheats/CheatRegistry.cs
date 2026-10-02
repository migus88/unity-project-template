#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Core.Cheats
{
    public sealed class CheatRegistry
    {
        public const string HelpName = "help";

        private const int MaxSuggestions = 3;
        private const int MaxSuggestionDistance = 2;

        public IReadOnlyList<ICheat> Cheats => _cheats;

        private readonly List<ICheat> _cheats = new();

        public CheatRegistry()
        {
            Add(new HelpCheat(this));
        }

        public IDisposable Add(ICheat cheat)
        {
            if (cheat == null)
            {
                throw new ArgumentNullException(nameof(cheat));
            }

            Validate(cheat);

            if (Find(cheat.Name) != null)
            {
                throw new InvalidOperationException($"The cheat '{cheat.Name}' is already registered.");
            }

            var index = 0;

            while (index < _cheats.Count && string.CompareOrdinal(_cheats[index].Name, cheat.Name) < 0)
            {
                index++;
            }

            _cheats.Insert(index, cheat);
            return Disposable.Create(() => _cheats.Remove(cheat));
        }

        public ICheat? Find(string name)
        {
            foreach (var cheat in _cheats)
            {
                if (cheat.Name == name)
                {
                    return cheat;
                }
            }

            return null;
        }

        public async UniTask<string> ExecuteAsync(string text, CancellationToken ct)
        {
            var line = CheatLine.Parse(text);

            if (line.IsEmpty)
            {
                return "Type help to list the cheats.";
            }

            var cheat = Find(line.Name);

            if (cheat == null)
            {
                return Unknown(line.Name);
            }

            return await CheatArgumentParser.Parse(cheat, line).Match(
                parsed => cheat.ExecuteAsync(parsed.Arguments, ct),
                invalid => UniTask.FromResult(invalid.Message));
        }

        private string Unknown(string name)
        {
            var suggestions = new List<string>();

            foreach (var cheat in _cheats)
            {
                if (suggestions.Count < MaxSuggestions && IsSimilar(cheat.Name, name))
                {
                    suggestions.Add(cheat.Name);
                }
            }

            return suggestions.Count == 0
                ? $"Unknown cheat '{name}'. Type help."
                : $"Unknown cheat '{name}'. Did you mean: {string.Join(", ", suggestions)}? Type help.";
        }

        private static bool IsSimilar(string candidate, string name)
        {
            return candidate.Contains(name, StringComparison.Ordinal)
                || Distance(candidate, name) <= Math.Min(MaxSuggestionDistance, name.Length / 2);
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;

                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }

        private static void Validate(ICheat cheat)
        {
            var name = cheat.Name;

            if (string.IsNullOrEmpty(name) || name != name.ToLowerInvariant() || name.IndexOfAny(new[] { ' ', '\t', '"', '`' }) >= 0)
            {
                throw new InvalidOperationException($"The cheat name '{name}' must be one lower-case word.");
            }

            var parameters = cheat.Parameters ?? throw new InvalidOperationException($"The cheat '{name}' has no parameter list.");
            var names = new HashSet<string>();
            var isOptionalSeen = false;

            foreach (var parameter in parameters)
            {
                if (!names.Add(parameter.Name))
                {
                    throw new InvalidOperationException($"The cheat '{name}' declares the parameter '{parameter.Name}' twice.");
                }

                if (isOptionalSeen && !parameter.IsOptional)
                {
                    throw new InvalidOperationException($"The cheat '{name}' declares the required parameter '{parameter.Name}' after an optional one.");
                }

                isOptionalSeen |= parameter.IsOptional;
            }
        }

        private sealed class HelpCheat : ICheat
        {
            public const string CheatParameterName = "cheat";

            public string Name => HelpName;

            public string Description => "Lists the cheats, or describes one.";

            public IReadOnlyList<CheatParameter> Parameters { get; }

            private readonly CheatRegistry _registry;

            public HelpCheat(CheatRegistry registry)
            {
                _registry = registry;
                Parameters = new[] { CheatParameter.Choice(CheatParameterName, Names).Optional() };
            }

            public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
            {
                if (!arguments.HasValue(CheatParameterName))
                {
                    return UniTask.FromResult(CheatHelpFormatter.List(_registry.Cheats));
                }

                var cheat = _registry.Find(arguments.GetText(CheatParameterName));
                return UniTask.FromResult(cheat == null ? string.Empty : CheatHelpFormatter.Describe(cheat));
            }

            private IEnumerable<string> Names()
            {
                foreach (var cheat in _registry.Cheats)
                {
                    yield return cheat.Name;
                }
            }
        }
    }
}
#endif
