using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    public sealed class StringFlag : RemoteFlag<string>
    {
        internal override string KindName => "string";

        public StringFlag(string key, string defaultValue) : base(key, defaultValue)
        {
        }

        internal override bool TryParse(JToken token, out string value)
        {
            if (token.Type == JTokenType.String)
            {
                value = token.Value<string>() ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        internal override bool TryParse(string text, out string value)
        {
            value = text;
            return true;
        }
    }
}
