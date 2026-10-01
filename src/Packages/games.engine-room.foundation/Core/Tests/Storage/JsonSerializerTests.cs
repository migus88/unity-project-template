using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Results;
using Core.Storage;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Storage
{
    public sealed class JsonSerializerTests
    {
        private JsonSerializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonSerializer();
        }

        [Test]
        public void Serialize_Record_UsesCamelCaseNames()
        {
            // Arrange
            var dto = new SampleDto("Ada", 3, "note", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));

            // Act
            var json = _serializer.Serialize(dto);

            // Assert
            json.Should().Contain("\"playerName\"").And.Contain("\"bestScore\"").And.NotContain("PlayerName");
        }

        [Test]
        public void Serialize_NullProperty_OmitsIt()
        {
            // Arrange
            var dto = new SampleDto("Ada", 3, null, DateTime.UtcNow);

            // Act
            var json = _serializer.Serialize(dto);

            // Assert
            json.Should().NotContain("note");
        }

        [Test]
        public void Serialize_Dictionary_KeepsKeysAsGiven()
        {
            // Arrange
            var sections = new Dictionary<string, int> { ["mainMenu"] = 1, ["Gameplay"] = 2 };

            // Act
            var json = _serializer.Serialize(sections);

            // Assert
            json.Should().Contain("\"mainMenu\"").And.Contain("\"Gameplay\"");
        }

        [Test]
        public void Serialize_ObjectGraph_EmitsNoTypeNames()
        {
            // Arrange
            var dto = new SampleDto("Ada", 3, null, DateTime.UtcNow);

            // Act
            var json = _serializer.Serialize(dto);

            // Assert
            json.Should().NotContain("$type");
        }

        [Test]
        public void Deserialize_SerializedRecord_RoundTrips()
        {
            // Arrange
            var dto = new SampleDto("Ada", 3, "note", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
            var json = _serializer.Serialize(dto);

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            result.Should().BeCase<SampleDto>().Which.Should().Be(dto);
        }

        [Test]
        public void Deserialize_OffsetTimestamp_ReturnsUtc()
        {
            // Arrange
            var json = "{\"playerName\":\"Ada\",\"bestScore\":1,\"savedAtUtc\":\"2026-09-28T14:00:00+02:00\"}";

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            var savedAt = result.Should().BeCase<SampleDto>().Which.SavedAtUtc;
            savedAt.Kind.Should().Be(DateTimeKind.Utc);
            savedAt.Should().Be(new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
        }

        [Test]
        public void Deserialize_MalformedJson_ReturnsCorrupted()
        {
            // Arrange
            var json = "{\"playerName\":";

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            result.Should().BeCase<Corrupted>().Which.Reason.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void Deserialize_TypeMismatch_ReturnsCorrupted()
        {
            // Arrange
            var json = "{\"playerName\":\"Ada\",\"bestScore\":\"many\"}";

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        [Test]
        public void Deserialize_NullLiteral_ReturnsCorrupted()
        {
            // Arrange
            var json = "null";

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        [Test]
        public void Deserialize_EmptyString_ReturnsCorrupted()
        {
            // Arrange
            var json = string.Empty;

            // Act
            var result = _serializer.Deserialize<SampleDto>(json);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        public sealed record SampleDto(string PlayerName, int BestScore, string? Note, DateTime SavedAtUtc);
    }
}
