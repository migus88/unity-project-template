#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Core.Cheats
{
    public static class CheatArgumentParser
    {
        private const int MaxListedValues = 20;

        public static CheatParse Parse(ICheat cheat, CheatLine line)
        {
            if (cheat == null)
            {
                throw new ArgumentNullException(nameof(cheat));
            }

            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            var parameters = cheat.Parameters;
            var values = new Dictionary<string, object>();

            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var tokenIndex = i + 1;

                if (tokenIndex >= line.Tokens.Count)
                {
                    if (parameter.IsOptional)
                    {
                        break;
                    }

                    return new CheatParse.Invalid($"Usage: {CheatHelpFormatter.Usage(cheat)}");
                }

                var isRestOfLine = IsRestOfLine(parameters, i);
                var text = isRestOfLine ? line.JoinValues(tokenIndex) : line.Tokens[tokenIndex].Value;

                if (!TryConvert(parameter, text, out var value, out var error))
                {
                    return new CheatParse.Invalid(error);
                }

                values[parameter.Name] = value;

                if (isRestOfLine)
                {
                    return new CheatParse.Parsed(new CheatArguments(values));
                }
            }

            if (line.Tokens.Count - 1 > parameters.Count)
            {
                return new CheatParse.Invalid($"Too many arguments. Usage: {CheatHelpFormatter.Usage(cheat)}");
            }

            return new CheatParse.Parsed(new CheatArguments(values));
        }

        public static bool IsRestOfLine(IReadOnlyList<CheatParameter> parameters, int index)
        {
            return index == parameters.Count - 1 && parameters[index].Kind is CheatValueKind.Text or CheatValueKind.Choice;
        }

        public static bool TryParseBool(string text, out bool value)
        {
            switch (text.ToLowerInvariant())
            {
                case "true":
                case "on":
                case "yes":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "off":
                case "no":
                case "0":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryConvert(CheatParameter parameter, string text, out object value, out string error)
        {
            value = text;
            error = string.Empty;

            switch (parameter.Kind)
            {
                case CheatValueKind.Int:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        value = number;
                        return true;
                    }

                    error = $"'{text}' is not a whole number for <{parameter.Name}>.";
                    return false;
                case CheatValueKind.Float:
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
                    {
                        value = real;
                        return true;
                    }

                    error = $"'{text}' is not a number for <{parameter.Name}>.";
                    return false;
                case CheatValueKind.Bool:
                    if (TryParseBool(text, out var flag))
                    {
                        value = flag;
                        return true;
                    }

                    error = $"'{text}' is not on or off for <{parameter.Name}>.";
                    return false;
                case CheatValueKind.Enum:
                    if (TryResolve(parameter, text, out var member, out error))
                    {
                        value = Enum.Parse(parameter.EnumType!, member);
                        return true;
                    }

                    return false;
                case CheatValueKind.Choice:
                    if (TryResolve(parameter, text, out var choice, out error))
                    {
                        value = choice;
                        return true;
                    }

                    return false;
                case CheatValueKind.Text:
                    return true;
                default:
                    throw new InvalidOperationException($"Unsupported cheat parameter kind {parameter.Kind}.");
            }
        }

        private static bool TryResolve(CheatParameter parameter, string text, out string value, out string error)
        {
            var known = parameter.Values();
            var matches = CheatValues.Matches(known, name => name, text);
            value = string.Empty;
            error = string.Empty;

            if (matches.Count == 1)
            {
                value = matches[0];
                return true;
            }

            error = matches.Count == 0
                ? $"No {parameter.Name} '{text}'. Values: {List(known)}."
                : $"'{text}' matches several {parameter.Name} values: {List(matches)}.";
            return false;
        }

        internal static string List(IReadOnlyList<string> values)
        {
            if (values.Count <= MaxListedValues)
            {
                return string.Join(", ", values);
            }

            var shown = new string[MaxListedValues];

            for (var i = 0; i < MaxListedValues; i++)
            {
                shown[i] = values[i];
            }

            return $"{string.Join(", ", shown)}, ... (+{values.Count - MaxListedValues})";
        }
    }
}
#endif
