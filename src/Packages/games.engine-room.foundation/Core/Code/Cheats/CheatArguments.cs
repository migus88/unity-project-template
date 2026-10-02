#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Core.Cheats
{
    public sealed class CheatArguments
    {
        public static readonly CheatArguments Empty = new(new Dictionary<string, object>());

        private readonly IReadOnlyDictionary<string, object> _values;

        public CheatArguments(IReadOnlyDictionary<string, object> values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public bool HasValue(string name)
        {
            return _values.ContainsKey(name);
        }

        public int GetInt(string name)
        {
            return Get<int>(name);
        }

        public int GetInt(string name, int defaultValue)
        {
            return HasValue(name) ? Get<int>(name) : defaultValue;
        }

        public float GetFloat(string name)
        {
            return Get<float>(name);
        }

        public float GetFloat(string name, float defaultValue)
        {
            return HasValue(name) ? Get<float>(name) : defaultValue;
        }

        public bool GetBool(string name)
        {
            return Get<bool>(name);
        }

        public bool GetBool(string name, bool defaultValue)
        {
            return HasValue(name) ? Get<bool>(name) : defaultValue;
        }

        public string GetText(string name)
        {
            return Get<string>(name);
        }

        public string GetText(string name, string defaultValue)
        {
            return HasValue(name) ? Get<string>(name) : defaultValue;
        }

        public TEnum GetEnum<TEnum>(string name) where TEnum : struct, Enum
        {
            return Get<TEnum>(name);
        }

        public TEnum GetEnum<TEnum>(string name, TEnum defaultValue) where TEnum : struct, Enum
        {
            return HasValue(name) ? Get<TEnum>(name) : defaultValue;
        }

        private T Get<T>(string name)
        {
            if (!_values.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"The cheat argument '{name}' has no value.");
            }

            if (value is not T typed)
            {
                throw new InvalidOperationException($"The cheat argument '{name}' is a {value.GetType().Name}, not a {typeof(T).Name}.");
            }

            return typed;
        }
    }
}
#endif
