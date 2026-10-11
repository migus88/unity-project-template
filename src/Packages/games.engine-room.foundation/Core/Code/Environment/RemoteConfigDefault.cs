using System;
using UnityEngine;

namespace Core.Environment
{
    [Serializable]
    public struct RemoteConfigDefault
    {
        [field: SerializeField] public string Key { get; private set; }
        [field: SerializeField] public string Value { get; private set; }

        public RemoteConfigDefault(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
