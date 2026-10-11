using System;
using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    public abstract class RemoteFlag<T>
    {
        public string Key { get; }

        public T Default { get; }

        internal abstract string KindName { get; }

        private protected RemoteFlag(string key, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Remote flag key must not be empty.", nameof(key));
            }

            Key = key;
            Default = defaultValue;
        }

        internal abstract bool TryParse(JToken token, out T value);
        internal abstract bool TryParse(string text, out T value);
    }
}
