using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Core.Analytics;
using Core.Logging;

namespace Core.Telemetry
{
    internal sealed class JsonAnalyticsWriter : IAnalyticsWriter
    {
        private const string PropLimitKey = "#limit";

        private string _eventName = string.Empty;
        private Dictionary<string, object> _props = new();

        private readonly HashSet<string> _reported = new();

        public TelemetryEvent? Write<TEvent>(TEvent analyticsEvent, long seq, long timeMs) where TEvent : struct, IAnalyticsEvent
        {
            var name = analyticsEvent.Name;

            if (name == null || !TelemetryLimits.NamePattern.IsMatch(name))
            {
                ReportOnce(name ?? string.Empty, string.Empty, $"Analytics event name '{name}' must match {TelemetryLimits.NamePattern}; the event is dropped.");
                return null;
            }

            _eventName = name;
            _props = new Dictionary<string, object>();
            analyticsEvent.Write(this);
            return new TelemetryEvent(name, seq, timeMs, _props);
        }

        public void Add(string key, string value)
        {
            if (CanAdd(key))
            {
                _props[key] = Truncate(value ?? string.Empty);
            }
        }

        public void Add(string key, int value)
        {
            if (CanAdd(key))
            {
                _props[key] = (long)value;
            }
        }

        public void Add(string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                ReportOnce(_eventName, key, $"Analytics event '{_eventName}' prop '{key}' is not a finite number; the prop is dropped.");
                return;
            }

            if (CanAdd(key))
            {
                _props[key] = double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }
        }

        public void Add(string key, bool value)
        {
            if (CanAdd(key))
            {
                _props[key] = value;
            }
        }

        private bool CanAdd(string key)
        {
            if (key == null || !TelemetryLimits.NamePattern.IsMatch(key))
            {
                ReportOnce(_eventName, key ?? string.Empty, $"Analytics event '{_eventName}' prop key '{key}' must match {TelemetryLimits.NamePattern}; the prop is dropped.");
                return false;
            }

            if (_props.Count >= TelemetryLimits.MaxProps && !_props.ContainsKey(key))
            {
                ReportOnce(_eventName, PropLimitKey, $"Analytics event '{_eventName}' has more than {TelemetryLimits.MaxProps} props; the extra props are dropped.");
                return false;
            }

            return true;
        }

        private void ReportOnce(string eventName, string key, string message)
        {
            if (_reported.Add(eventName + "/" + key))
            {
                Log.Error(LogTags.Telemetry, message);
            }
        }

        private static string Truncate(string value)
        {
            if (Encoding.UTF8.GetByteCount(value) <= TelemetryLimits.MaxStringBytes)
            {
                return value;
            }

            var bytes = 0;
            var length = 0;

            while (length < value.Length)
            {
                var isPair = char.IsHighSurrogate(value[length]) && length + 1 < value.Length && char.IsLowSurrogate(value[length + 1]);
                var charCount = isPair ? 2 : 1;
                var charBytes = Encoding.UTF8.GetByteCount(value.ToCharArray(length, charCount));

                if (bytes + charBytes > TelemetryLimits.MaxStringBytes)
                {
                    break;
                }

                bytes += charBytes;
                length += charCount;
            }

            return value.Substring(0, length);
        }
    }
}
