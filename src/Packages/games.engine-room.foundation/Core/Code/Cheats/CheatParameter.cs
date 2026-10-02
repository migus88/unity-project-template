#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Cheats
{
    public sealed record CheatParameter
    {
        public static readonly IReadOnlyList<string> BoolWords = new[] { "on", "off" };

        public string Name { get; }

        public CheatValueKind Kind { get; }

        public Type? EnumType { get; }

        public bool IsOptional { get; private init; }

        private readonly Func<IEnumerable<string>>? _values;

        private CheatParameter(string name, CheatValueKind kind, Type? enumType, Func<IEnumerable<string>>? values)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A cheat parameter needs a name.", nameof(name));
            }

            Name = name;
            Kind = kind;
            EnumType = enumType;
            _values = values;
        }

        public static CheatParameter Int(string name)
        {
            return new CheatParameter(name, CheatValueKind.Int, null, null);
        }

        public static CheatParameter Float(string name)
        {
            return new CheatParameter(name, CheatValueKind.Float, null, null);
        }

        public static CheatParameter Bool(string name)
        {
            return new CheatParameter(name, CheatValueKind.Bool, null, null);
        }

        public static CheatParameter Enum<TEnum>(string name) where TEnum : struct, System.Enum
        {
            return new CheatParameter(name, CheatValueKind.Enum, typeof(TEnum), null);
        }

        public static CheatParameter Text(string name, Func<IEnumerable<string>>? suggestions = null)
        {
            return new CheatParameter(name, CheatValueKind.Text, null, suggestions);
        }

        public static CheatParameter Choice(string name, Func<IEnumerable<string>> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            return new CheatParameter(name, CheatValueKind.Choice, null, values);
        }

        public CheatParameter Optional()
        {
            return this with { IsOptional = true };
        }

        public IReadOnlyList<string> Values()
        {
            switch (Kind)
            {
                case CheatValueKind.Bool:
                    return BoolWords;
                case CheatValueKind.Enum:
                    return System.Enum.GetNames(EnumType!);
                case CheatValueKind.Text:
                case CheatValueKind.Choice:
                    return _values == null ? Array.Empty<string>() : _values().Where(value => !string.IsNullOrEmpty(value)).ToArray();
                default:
                    return Array.Empty<string>();
            }
        }
    }
}
#endif
