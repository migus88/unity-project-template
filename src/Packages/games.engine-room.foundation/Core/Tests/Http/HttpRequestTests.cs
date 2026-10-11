using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Core.Http;
using NUnit.Framework;

namespace Core.Tests.Http
{
    public sealed class HttpRequestTests
    {
        private const string Url = "https://example.test/events";
        private const string Json = "{\"name\":\"run_started\"}";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Test]
        public void Get_WithETag_SetsIfNoneMatch()
        {
            // Act
            var request = HttpRequest.Get(Url, Timeout, "\"v1\"");

            // Assert
            request.Headers.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>(HttpRequest.IfNoneMatchHeader, "\"v1\""));
        }

        [Test]
        public void Get_WithoutETag_SetsNoHeader()
        {
            // Act
            var request = HttpRequest.Get(Url, Timeout);

            // Assert
            request.Headers.Should().BeEmpty();
        }

        [Test]
        public void PostJson_Gzipped_SetsEncodingAndGunzipsToJson()
        {
            // Act
            var request = HttpRequest.PostJson(Url, Json, true, Timeout);

            // Assert
            request.Headers.Should().ContainSingle().Which.Value.Should().Be(HttpRequest.GzipEncoding);
            request.ContentType.Should().Be(HttpRequest.JsonContentType);
            Encoding.UTF8.GetString(Gunzip(request.Body!)).Should().Be(Json);
        }

        [Test]
        public void PostJson_Plain_RawUtf8BodyWithoutEncoding()
        {
            // Act
            var request = HttpRequest.PostJson(Url, Json, false, Timeout);

            // Assert
            request.Headers.Should().BeEmpty();
            request.Body.Should().Equal(Encoding.UTF8.GetBytes(Json));
        }

        [Test]
        public void WithHeader_Original_LeftUntouched()
        {
            // Arrange
            var original = HttpRequest.Get(Url, Timeout);

            // Act
            var copy = original.WithHeader("X-Ingest-Key", "secret");

            // Assert
            original.Headers.Should().BeEmpty();
            copy.Headers.Should().ContainSingle().Which.Key.Should().Be("X-Ingest-Key");
        }

        [TestCase("", 5)]
        [TestCase(Url, 0)]
        public void Constructor_EmptyUrlOrNonPositiveTimeout_Throws(string url, int timeoutSeconds)
        {
            // Act
            Action act = () => _ = new HttpRequest(HttpMethod.Get, url, TimeSpan.FromSeconds(timeoutSeconds));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        private static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data, false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
