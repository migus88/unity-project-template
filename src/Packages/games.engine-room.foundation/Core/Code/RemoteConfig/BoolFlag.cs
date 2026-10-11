using System;
using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    public sealed class BoolFlag : RemoteFlag<bool>
    {
        internal override string KindName => "bool";

        public BoolFlag(string key, bool defaultValue) : base(key, defaultValue)
        {
        }

        internal override bool TryParse(JToken token, out bool value)
        {
            if (token.Type == JTokenType.Boolean)
            {
                value = token.Value<bool>();
                return true;
            }

            value = false;
            return false;
        }

        internal override bool TryParse(string text, out bool value)
        {
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            value = false;
            return string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
