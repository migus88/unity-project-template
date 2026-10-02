#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;

namespace Core.Cheats
{
    internal static class CheatHelpFormatter
    {
        public const char Column = '\t';

        private const int MaxInlineEnumValues = 4;

        public static string Usage(ICheat cheat)
        {
            var arguments = Arguments(cheat);
            return arguments.Length == 0 ? cheat.Name : string.Concat(cheat.Name, " ", arguments);
        }

        public static string Arguments(ICheat cheat)
        {
            var arguments = new StringBuilder();

            foreach (var parameter in cheat.Parameters)
            {
                if (arguments.Length > 0)
                {
                    arguments.Append(' ');
                }

                arguments.Append(parameter.IsOptional ? '[' : '<');
                arguments.Append(Placeholder(parameter));
                arguments.Append(parameter.IsOptional ? ']' : '>');
            }

            return arguments.ToString();
        }

        public static string List(IReadOnlyList<ICheat> cheats)
        {
            var list = new StringBuilder();

            foreach (var cheat in cheats)
            {
                if (list.Length > 0)
                {
                    list.Append('\n');
                }

                list.Append(cheat.Name).Append(Column).Append(Arguments(cheat)).Append(Column).Append(cheat.Description);
            }

            return list.ToString();
        }

        public static string Describe(ICheat cheat)
        {
            var description = new StringBuilder();
            description.Append("Usage: ").Append(Usage(cheat)).Append('\n').Append(cheat.Description);

            foreach (var parameter in cheat.Parameters)
            {
                description.Append('\n').Append(Placeholder(parameter, true)).Append(Column).Append(ValuesOf(parameter));
            }

            return description.ToString();
        }

        private static string Placeholder(CheatParameter parameter, bool isNamed = false)
        {
            if (isNamed)
            {
                return parameter.IsOptional ? $"[{parameter.Name}]" : $"<{parameter.Name}>";
            }

            switch (parameter.Kind)
            {
                case CheatValueKind.Bool:
                    return string.Join("|", CheatParameter.BoolWords);
                case CheatValueKind.Enum:
                    var names = parameter.Values();
                    return names.Count <= MaxInlineEnumValues ? string.Join("|", names) : parameter.Name;
                default:
                    return parameter.Name;
            }
        }

        private static string ValuesOf(CheatParameter parameter)
        {
            var optional = parameter.IsOptional ? " (optional)" : string.Empty;

            switch (parameter.Kind)
            {
                case CheatValueKind.Int:
                    return "whole number" + optional;
                case CheatValueKind.Float:
                    return "number" + optional;
                case CheatValueKind.Bool:
                    return "on or off" + optional;
                case CheatValueKind.Text:
                    var suggestions = parameter.Values();
                    return suggestions.Count == 0 ? "text" + optional : $"text, e.g. {CheatArgumentParser.List(suggestions)}{optional}";
                default:
                    return CheatArgumentParser.List(parameter.Values()) + optional;
            }
        }
    }
}
#endif
