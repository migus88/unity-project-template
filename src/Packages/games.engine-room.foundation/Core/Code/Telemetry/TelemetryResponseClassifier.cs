using System;
using System.Globalization;
using Core.Http;

namespace Core.Telemetry
{
    internal static class TelemetryResponseClassifier
    {
        private const long RequestTimeoutStatus = 408;
        private const long TooManyRequestsStatus = 429;
        private const long ServerErrorStatus = 500;
        private const string RetryAfterHeader = "Retry-After";

        public static TelemetrySendOutcome Classify(HttpResult result)
        {
            return result.Match<TelemetrySendOutcome>(
                _ => new TelemetrySendOutcome.Sent(),
                error => ClassifyError(error.Response),
                network => new TelemetrySendOutcome.Retry(null, $"network error: {network.Message}"),
                timeout => new TelemetrySendOutcome.Retry(null, $"timed out after {timeout.After.TotalSeconds:0.#} s"));
        }

        private static TelemetrySendOutcome ClassifyError(HttpResponse response)
        {
            var status = response.StatusCode;

            if (status == RequestTimeoutStatus || status == TooManyRequestsStatus || status >= ServerErrorStatus)
            {
                return new TelemetrySendOutcome.Retry(ReadRetryAfter(response), $"HTTP {status}");
            }

            return new TelemetrySendOutcome.Rejected(status, response.ReadText());
        }

        private static TimeSpan? ReadRetryAfter(HttpResponse response)
        {
            var value = response.GetHeader(RetryAfterHeader);

            if (value != null && int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }
    }
}
