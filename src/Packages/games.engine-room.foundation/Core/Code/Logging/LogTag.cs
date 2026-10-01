using System;
using UnityEngine;

namespace Core.Logging
{
    [Serializable]
    public struct LogTag
    {
        public readonly string Name => _name ?? string.Empty;

        [SerializeField] private string _name;

        public LogTag(string name)
        {
            _name = name;
        }
    }
}
