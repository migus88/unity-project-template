using System.Globalization;
using System.Text;

namespace Core.Analytics
{
    internal sealed class AnalyticsLine : IAnalyticsWriter
    {
        private int _count;

        private readonly StringBuilder _builder = new();

        public string Format<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
            _builder.Clear();
            _count = 0;
            _builder.Append(analyticsEvent.Name).Append(" {");
            analyticsEvent.Write(this);
            _builder.Append('}');
            return _builder.ToString();
        }

        public void Add(string key, string value)
        {
            AppendKey(key).Append(value);
        }

        public void Add(string key, int value)
        {
            AppendKey(key).Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Add(string key, float value)
        {
            AppendKey(key).Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public void Add(string key, bool value)
        {
            AppendKey(key).Append(value ? "true" : "false");
        }

        private StringBuilder AppendKey(string key)
        {
            if (_count > 0)
            {
                _builder.Append(", ");
            }

            _count++;
            return _builder.Append(key).Append('=');
        }
    }
}
