using System.Linq;
using AwesomeAssertions;
using Core.Http;
using Core.Storage;
using Core.Telemetry;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryBatchRequestTests
    {
        [Test]
        public void Create_EndpointWithTrailingSlash_PostsToEventsPath()
        {
            // Act
            var request = TelemetryBatchRequest.Create(TelemetryTestData.Options, "{}", TelemetryLimits.RequestTimeout);

            // Assert
            request.Method.Should().Be(HttpMethod.Post);
            request.Url.Should().Be("https://telemetry.test/v1/events");
        }

        [TestCase("ingest-key", true)]
        [TestCase("", false)]
        public void Create_IngestKey_SendsHeaderOnlyWhenSet(string key, bool hasHeader)
        {
            // Arrange
            var options = TelemetryTestData.Options with { IngestKey = key };

            // Act
            var request = TelemetryBatchRequest.Create(options, "{}", TelemetryLimits.RequestTimeout);

            // Assert
            request.Headers.Any(header => header.Key == TelemetryBatchRequest.IngestKeyHeader && header.Value == key).Should().Be(hasHeader);
        }

        [Test]
        public void Create_Batch_GzipsTheWireShape()
        {
            // Arrange
            var telemetryEvent = TelemetryTestData.Event("battle_ended", 3);
            telemetryEvent.Props["turns"] = 7L;
            var batch = new TelemetryBatch(TelemetryTestData.Envelope(), new[] { telemetryEvent });
            var json = TelemetryBatchRequest.ToJson(batch, new JsonSerializer());

            // Act
            var request = TelemetryBatchRequest.Create(TelemetryTestData.Options, json, TelemetryLimits.RequestTimeout);

            // Assert
            var body = TelemetryTestData.ReadBody(request);
            var envelope = body["envelope"]!;
            envelope["installId"]!.Value<string>().Should().Be(TelemetryTestData.Envelope().InstallId);
            envelope["sessionId"]!.Value<string>().Should().Be(TelemetryTestData.Envelope().SessionId);
            envelope["build"]!.Value<string>().Should().Be("0.1.0");
            envelope["platform"]!.Value<string>().Should().Be("OSXPlayer");
            envelope["environment"]!.Value<string>().Should().Be("staging");
            envelope["policyVersion"]!.Value<int>().Should().Be(1);
            var wireEvent = body["events"]!.Single();
            wireEvent["name"]!.Value<string>().Should().Be("battle_ended");
            wireEvent["seq"]!.Value<long>().Should().Be(3);
            wireEvent["time"]!.Value<long>().Should().Be(1003);
            wireEvent["props"]!["turns"]!.Type.Should().Be(JTokenType.Integer);
        }
    }
}
