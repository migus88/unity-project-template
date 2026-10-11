using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    public sealed class IntFlag : RemoteFlag<int>
    {
        internal override string KindName => "int";

        public IntFlag(string key, int defaultValue) : base(key, defaultValue)
        {
        }

        internal override bool TryParse(JToken token, out int value)
        {
            value = 0;

            if (token.Type != JTokenType.Integer || token is not JValue { Value: long number })
            {
                return false;
            }

            if (number < int.MinValue || number > int.MaxValue)
            {
                return false;
            }

            value = (int)number;
            return true;
        }

        internal override bool TryParse(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
