#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Core.Cheats
{
    internal sealed class CheatHistory
    {
        public const int Capacity = 50;

        public int Count => _entries.Count;

        private int _cursor;
        private string _draft = string.Empty;

        private readonly List<string> _entries = new();

        public void Add(string line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0 && (_entries.Count == 0 || _entries[^1] != trimmed))
            {
                _entries.Add(trimmed);

                if (_entries.Count > Capacity)
                {
                    _entries.RemoveAt(0);
                }
            }

            _cursor = _entries.Count;
            _draft = string.Empty;
        }

        public string? Previous(string currentLine)
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            if (_cursor == _entries.Count)
            {
                _draft = currentLine;
            }

            if (_cursor > 0)
            {
                _cursor--;
            }

            return _entries[_cursor];
        }

        public string? Next()
        {
            if (_cursor >= _entries.Count)
            {
                return null;
            }

            _cursor++;
            return _cursor == _entries.Count ? _draft : _entries[_cursor];
        }
    }
}
#endif
