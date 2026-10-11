using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Http
{
    public sealed record HttpResponse
    {
        private const long NotModifiedStatus = 304;
        private const string ETagHeader = "ETag";

        public long StatusCode { get; }

        public byte[] Body { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public string? ETag => GetHeader(ETagHeader);

        public bool IsNotModified => StatusCode == NotModifiedStatus;

        public HttpResponse(long statusCode, byte[] body, IReadOnlyDictionary<string, string> headers)
        {
            StatusCode = statusCode;
            Body = body;
            var caseInsensitive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                caseInsensitive[header.Key] = header.Value;
            }

            Headers = caseInsensitive;
        }

        public string? GetHeader(string name)
        {
            return Headers.TryGetValue(name, out var value) ? value : null;
        }

        public string ReadText()
        {
            return Encoding.UTF8.GetString(Body);
        }
    }
}
