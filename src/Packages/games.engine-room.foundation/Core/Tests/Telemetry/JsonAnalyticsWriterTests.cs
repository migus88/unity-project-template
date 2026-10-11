using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Core.Telemetry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Telemetry
{
    public sealed class JsonAnalyticsWriterTests
    {
        private JsonAnalyticsWriter _writer = null!;

        [SetUp]
        public void SetUp()
        {
            _writer = new JsonAnalyticsWriter();
        }

        [Test]
        public void Write_TypedProps_KeepsJsonTypesAndStamps()
        {
            // Arrange
            var analyticsEvent = new TestTelemetryEvent("battle_ended", writer =>
            {
                writer.Add("mech_id", "atlas");
                writer.Add("turns", 7);
                writer.Add("ratio", 0.1f);
                writer.Add("is_won", true);
            });

            // Act
            var written = _writer.Write(analyticsEvent, 3, 1_700_000_000_000);

            // Assert
            written.Should().NotBeNull();
            written!.Seq.Should().Be(3);
            written.Time.Should().Be(1_700_000_000_000);
            written.Props["mech_id"].Should().Be("atlas");
            written.Props["turns"].Should().Be(7L);
            written.Props["ratio"].Should().Be(0.1d);
            written.Props["is_won"].Should().Be(true);
        }

        [Test]
        public void Write_InvalidKey_DropsPropAndLogsError()
        {
            // Arrange
            var analyticsEvent = new TestTelemetryEvent("battle_ended", writer =>
            {
                writer.Add("MechId", "atlas");
                writer.Add("turns", 7);
            });
            LogAssert.Expect(LogType.Error, new Regex("prop key 'MechId'"));

            // Act
            var written = _writer.Write(analyticsEvent, 0, 1);

            // Assert
            written!.Props.Keys.Should().Equal("turns");
        }

        [Test]
        public void Write_NonFiniteFloat_DropsPropAndLogsError()
        {
            // Arrange
            var analyticsEvent = new TestTelemetryEvent("battle_ended", writer => writer.Add("ratio", float.NaN));
            LogAssert.Expect(LogType.Error, new Regex("not a finite number"));

            // Act
            var written = _writer.Write(analyticsEvent, 0, 1);

            // Assert
            written!.Props.Should().BeEmpty();
        }

        [Test]
        public void Write_LongMultibyteString_CutsAtByteLimitWithoutSplittingACharacter()
        {
            // Arrange
            var value = "a" + new string('é', 200);
            var analyticsEvent = new TestTelemetryEvent("battle_ended", writer => writer.Add("label", value));

            // Act
            var written = _writer.Write(analyticsEvent, 0, 1);

            // Assert
            var label = (string)written!.Props["label"];
            Encoding.UTF8.GetByteCount(label).Should().Be(255);
            label.Should().Be(value.Substring(0, 128));
        }

        [Test]
        public void Write_InvalidEventName_ReturnsNullAndLogsError()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("event name 'BattleEnded'"));

            // Act
            var written = _writer.Write(new TestTelemetryEvent("BattleEnded"), 0, 1);

            // Assert
            written.Should().BeNull();
        }

        [Test]
        public void Write_MoreThanMaxProps_DropsTheExtraProps()
        {
            // Arrange
            var analyticsEvent = new TestTelemetryEvent("battle_ended", writer =>
            {
                for (var i = 0; i <= TelemetryLimits.MaxProps; i++)
                {
                    writer.Add($"prop_{i}", i);
                }
            });
            LogAssert.Expect(LogType.Error, new Regex("more than 64 props"));

            // Act
            var written = _writer.Write(analyticsEvent, 0, 1);

            // Assert
            written!.Props.Should().HaveCount(TelemetryLimits.MaxProps).And.NotContainKey($"prop_{TelemetryLimits.MaxProps}");
        }
    }
}
