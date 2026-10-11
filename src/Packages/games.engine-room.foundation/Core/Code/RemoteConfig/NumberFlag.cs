using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    public sealed class NumberFlag : RemoteFlag<double>
    {
        internal override string KindName => "number";

        public NumberFlag(string key, double defaultValue) : base(key, defaultValue)
        {
        }

        internal override bool TryParse(JToken token, out double value)
        {
            value = 0d;

            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                return false;
            }

            var number = token.Value<double>();

            if (!double.IsFinite(number))
            {
                return false;
            }

            value = number;
            return true;
        }

        internal override bool TryParse(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                value = number;
                return true;
            }

            value = 0d;
            return false;
        }
    }
}
