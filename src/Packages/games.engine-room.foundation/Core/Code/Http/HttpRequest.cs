using System;
using System.Collections.Generic;
using System.Text;
using Core.Compression;

namespace Core.Http
{
    public sealed record HttpRequest
    {
        public const string IfNoneMatchHeader = "If-None-Match";
        public const string ContentEncodingHeader = "Content-Encoding";
        public const string GzipEncoding = "gzip";
        public const string JsonContentType = "application/json";

        public static HttpRequest Get(string url, TimeSpan timeout, string? etag = null)
        {
            var request = new HttpRequest(HttpMethod.Get, url, timeout);
            return string.IsNullOrEmpty(etag) ? request : request.WithHeader(IfNoneMatchHeader, etag!);
        }

        public static HttpRequest PostJson(string url, string json, bool isGzipped, TimeSpan timeout)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var request = new HttpRequest(HttpMethod.Post, url, timeout)
            {
                Body = isGzipped ? Gzip.Compress(body) : body,
                ContentType = JsonContentType,
            };

            return isGzipped ? request.WithHeader(ContentEncodingHeader, GzipEncoding) : request;
        }

        public static HttpRequest PutBytes(string url, byte[] bytes, string contentType, TimeSpan timeout)
        {
            return new HttpRequest(HttpMethod.Put, url, timeout)
            {
                Body = bytes,
                ContentType = contentType,
            };
        }

        public HttpMethod Method { get; }

        public string Url { get; }

        public TimeSpan Timeout { get; }

        public byte[]? Body { get; init; }

        public string? ContentType { get; init; }

        public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = Array.Empty<KeyValuePair<string, string>>();

        public HttpRequest(HttpMethod method, string url, TimeSpan timeout)
        {
            if (method == HttpMethod.None)
            {
                throw new ArgumentException("HTTP method must be set.", nameof(method));
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL must not be empty.", nameof(url));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentException($"Timeout must be positive: {timeout}.", nameof(timeout));
            }

            Method = method;
            Url = url;
            Timeout = timeout;
        }

        public HttpRequest WithHeader(string name, string value)
        {
            var headers = new List<KeyValuePair<string, string>>(Headers) { new(name, value) };
            return this with { Headers = headers };
        }
    }
}
