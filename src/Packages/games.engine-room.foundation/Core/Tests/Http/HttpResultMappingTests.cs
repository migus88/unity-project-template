using System;
using System.Collections.Generic;
using System.Text;
using AwesomeAssertions;
using Core.Http;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Http
{
    public sealed class HttpResultMappingTests
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

        [Test]
        public void ToResult_Success200_CompletedWithResponse()
        {
            // Arrange
            var body = Encoding.UTF8.GetBytes("ok");
            var raw = new HttpTransportResponse(HttpTransportOutcome.Success, 200, body, NoHeaders, string.Empty);

            // Act
            var result = HttpResultMapping.ToResult(raw);

            // Assert
            var response = result.Should().BeCase<HttpResult.Completed>().Which.Response;
            response.StatusCode.Should().Be(200);
            response.ReadText().Should().Be("ok");
        }

        [Test]
        public void ToResult_Success304_CompletedNotModified()
        {
            // Arrange
            var raw = new HttpTransportResponse(HttpTransportOutcome.Success, 304, Array.Empty<byte>(), NoHeaders, string.Empty);

            // Act
            var result = HttpResultMapping.ToResult(raw);

            // Assert
            result.Should().BeCase<HttpResult.Completed>().Which.Response.IsNotModified.Should().BeTrue();
        }

        [TestCase((int)HttpTransportOutcome.ProtocolError, 404)]
        [TestCase((int)HttpTransportOutcome.ProtocolError, 503)]
        [TestCase((int)HttpTransportOutcome.Success, 400)]
        public void ToResult_ErrorStatus_HttpErrorKeepingResponse(int outcome, long status)
        {
            // Arrange
            var headers = new Dictionary<string, string> { ["Retry-After"] = "30" };
            var raw = new HttpTransportResponse((HttpTransportOutcome)outcome, status, Encoding.UTF8.GetBytes("nope"), headers, "HTTP/1.1 error");

            // Act
            var result = HttpResultMapping.ToResult(raw);

            // Assert
            var response = result.Should().BeCase<HttpResult.HttpError>().Which.Response;
            response.StatusCode.Should().Be(status);
            response.ReadText().Should().Be("nope");
            response.GetHeader("Retry-After").Should().Be("30");
        }

        [Test]
        public void ToResult_Success399_Completed()
        {
            // Arrange
            var raw = new HttpTransportResponse(HttpTransportOutcome.Success, 399, Array.Empty<byte>(), NoHeaders, string.Empty);

            // Act
            var result = HttpResultMapping.ToResult(raw);

            // Assert
            result.Should().BeCase<HttpResult.Completed>();
        }

        [TestCase((int)HttpTransportOutcome.ConnectionError)]
        [TestCase((int)HttpTransportOutcome.DataProcessingError)]
        public void ToResult_TransportFailure_NetworkErrorWithMessage(int outcome)
        {
            // Arrange
            var raw = new HttpTransportResponse((HttpTransportOutcome)outcome, 0, Array.Empty<byte>(), NoHeaders, "Cannot resolve destination host");

            // Act
            var result = HttpResultMapping.ToResult(raw);

            // Assert
            result.Should().BeCase<HttpResult.NetworkError>().Which.Message.Should().Be("Cannot resolve destination host");
        }
    }
}
