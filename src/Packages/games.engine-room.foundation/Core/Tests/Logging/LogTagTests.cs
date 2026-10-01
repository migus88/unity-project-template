using AwesomeAssertions;
using Core.Logging;
using NUnit.Framework;
using UnityEngine;

namespace Core.Tests.Logging
{
    public sealed class LogTagTests
    {
        [Test]
        public void Name_Default_IsEmpty()
        {
            // Arrange
            var tag = default(LogTag);

            // Act
            var name = tag.Name;

            // Assert
            name.Should().BeEmpty();
        }

        [Test]
        public void UnitySerialization_RoundTrip_KeepsName()
        {
            // Arrange
            var tag = new LogTag("Gameplay");

            // Act
            var restored = JsonUtility.FromJson<LogTag>(JsonUtility.ToJson(tag));

            // Assert
            restored.Name.Should().Be("Gameplay");
        }
    }
}
