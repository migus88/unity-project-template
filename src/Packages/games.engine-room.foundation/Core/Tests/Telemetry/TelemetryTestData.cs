using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Core.Http;
using Core.Telemetry;
using Newtonsoft.Json.Linq;

namespace Core.Tests.Telemetry
{
    internal static class TelemetryTestData
    {
        public static readonly TelemetryOptions Options = new(true, "https://telemetry.test/", "ingest-key", "staging", "0.1.0", "OSXPlayer");

        public static TelemetryEnvelope Envelope(string sessionId = "00000000-0000-0000-0000-000000000001")
        {
            return new TelemetryEnvelope("00000000-0000-0000-0000-0000000000aa", sessionId, "0.1.0", "OSXPlayer", "staging", 1);
        }

        public static TelemetryEvent Event(string name, long seq = 0)
        {
            return new TelemetryEvent(name, seq, 1_000 + seq, new Dictionary<string, object>());
        }

        public static HttpResult Status(long status, string? retryAfter = null)
        {
            var headers = new Dictionary<string, string>();

            if (retryAfter != null)
            {
                headers["Retry-After"] = retryAfter;
            }

            var response = new HttpResponse(status, Encoding.UTF8.GetBytes("{}"), headers);
            return status < 400 ? new HttpResult.Completed(response) : new HttpResult.HttpError(response);
        }

        public static JObject ReadBody(HttpRequest request)
        {
            using var input = new MemoryStream(request.Body!);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return JObject.Parse(reader.ReadToEnd());
        }
    }
}
