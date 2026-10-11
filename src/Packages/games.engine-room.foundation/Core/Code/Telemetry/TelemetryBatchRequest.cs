using System;
using Core.Http;
using Core.Storage;

namespace Core.Telemetry
{
    internal static class TelemetryBatchRequest
    {
        public const string IngestKeyHeader = "X-Ingest-Key";
        public const string EventsPath = "/v1/events";

        public static string ToJson(TelemetryBatch batch, IJsonSerializer serializer)
        {
            return serializer.Serialize(batch);
        }

        public static HttpRequest Create(TelemetryOptions options, string json, TimeSpan timeout)
        {
            var request = HttpRequest.PostJson(options.Endpoint.TrimEnd('/') + EventsPath, json, true, timeout);
            return string.IsNullOrEmpty(options.IngestKey) ? request : request.WithHeader(IngestKeyHeader, options.IngestKey);
        }
    }
}
