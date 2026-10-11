using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Http;
using NUnit.Framework;

namespace Core.Tests.Http
{
    public sealed class HttpResponseTests
    {
        [Test]
        public void GetHeader_DifferentCase_Found()
        {
            // Arrange
            var response = new HttpResponse(200, Array.Empty<byte>(), new Dictionary<string, string> { ["etag"] = "\"v2\"" });

            // Act
            var etag = response.ETag;

            // Assert
            etag.Should().Be("\"v2\"");
        }

        [Test]
        public void GetHeader_Missing_Null()
        {
            // Arrange
            var response = new HttpResponse(200, Array.Empty<byte>(), new Dictionary<string, string>());

            // Act
            var value = response.GetHeader("Retry-After");

            // Assert
            value.Should().BeNull();
        }
    }
}
