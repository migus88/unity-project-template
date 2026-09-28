using System;
using UnityEngine;

namespace Core.Localization
{
    [Serializable]
    public struct TextKey : IEquatable<TextKey>
    {
        public readonly string Table => _table ?? string.Empty;
        public readonly string Key => _key ?? string.Empty;

        [SerializeField] private string _table;
        [SerializeField] private string _key;

        public TextKey(string table, string key)
        {
            _table = table;
            _key = key;
        }

        public readonly bool Equals(TextKey other)
        {
            return string.Equals(Table, other.Table, StringComparison.Ordinal) && string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is TextKey other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Table, Key);
        }

        public override readonly string ToString()
        {
            return $"{Table}/{Key}";
        }
    }
}
