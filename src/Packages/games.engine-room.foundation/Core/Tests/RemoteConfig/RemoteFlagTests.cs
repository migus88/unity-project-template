using System;
using AwesomeAssertions;
using Core.RemoteConfig;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Core.Tests.RemoteConfig
{
    public sealed class RemoteFlagTests
    {
        [TestCase("true", true)]
        [TestCase("\"true\"", false)]
        public void BoolFlag_TryParseToken_AcceptsOnlyBooleanTokens(string json, bool expected)
        {
            // Arrange
            var flag = new BoolFlag("flag", false);

            // Act
            var parsed = flag.TryParse(JToken.Parse(json), out _);

            // Assert
            parsed.Should().Be(expected);
        }

        [TestCase("12", true, 12)]
        [TestCase("1.5", false, 0)]
        [TestCase("3000000000", false, 0)]
        public void IntFlag_TryParseToken_AcceptsOnlyIntegersInRange(string json, bool expected, int expectedValue)
        {
            // Arrange
            var flag = new IntFlag("flag", 0);

            // Act
            var parsed = flag.TryParse(JToken.Parse(json), out var value);

            // Assert
            parsed.Should().Be(expected);
            value.Should().Be(expectedValue);
        }

        [TestCase("2", 2d)]
        [TestCase("0.25", 0.25d)]
        public void NumberFlag_TryParseToken_AcceptsIntegerAndFloatTokens(string json, double expected)
        {
            // Arrange
            var flag = new NumberFlag("flag", 0d);

            // Act
            var parsed = flag.TryParse(JToken.Parse(json), out var value);

            // Assert
            parsed.Should().BeTrue();
            value.Should().BeApproximately(expected, 1e-9);
        }

        [TestCase("\"hello\"", true)]
        [TestCase("5", false)]
        public void StringFlag_TryParseToken_AcceptsOnlyStringTokens(string json, bool expected)
        {
            // Arrange
            var flag = new StringFlag("flag", string.Empty);

            // Act
            var parsed = flag.TryParse(JToken.Parse(json), out _);

            // Assert
            parsed.Should().Be(expected);
        }

        [TestCase("TRUE", true, true)]
        [TestCase("yes", false, false)]
        public void BoolFlag_TryParseText_IgnoresCaseAndRejectsOtherWords(string text, bool expected, bool expectedValue)
        {
            // Arrange
            var flag = new BoolFlag("flag", false);

            // Act
            var parsed = flag.TryParse(text, out var value);

            // Assert
            parsed.Should().Be(expected);
            value.Should().Be(expectedValue);
        }

        [TestCase("-7", true)]
        [TestCase("7.0", false)]
        public void IntFlag_TryParseText_AcceptsOnlyIntegers(string text, bool expected)
        {
            // Arrange
            var flag = new IntFlag("flag", 0);

            // Act
            var parsed = flag.TryParse(text, out _);

            // Assert
            parsed.Should().Be(expected);
        }

        [TestCase("1.5", true)]
        [TestCase("NaN", false)]
        public void NumberFlag_TryParseText_UsesInvariantCultureAndRejectsNonFinite(string text, bool expected)
        {
            // Arrange
            var flag = new NumberFlag("flag", 0d);

            // Act
            var parsed = flag.TryParse(text, out var value);

            // Assert
            parsed.Should().Be(expected);
            value.Should().BeApproximately(expected ? 1.5d : 0d, 1e-9);
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_EmptyKey_Throws(string key)
        {
            // Act
            Action act = () => _ = new StringFlag(key, "value");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
