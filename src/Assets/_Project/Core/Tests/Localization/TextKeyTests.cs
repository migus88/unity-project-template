using AwesomeAssertions;
using Core.Localization;
using NUnit.Framework;

namespace Core.Tests.Localization
{
    public sealed class TextKeyTests
    {
        [Test]
        public void Equals_SameTableAndKey_IsTrue()
        {
            // Arrange
            var first = new TextKey("Shared", "play");
            var second = new TextKey("Shared", "play");

            // Act
            var isEqual = first.Equals(second);

            // Assert
            isEqual.Should().BeTrue();
            first.GetHashCode().Should().Be(second.GetHashCode());
        }

        [Test]
        public void Equals_DifferentTable_IsFalse()
        {
            // Arrange
            var first = new TextKey("Shared", "play");
            var second = new TextKey("MainMenu", "play");

            // Act
            var isEqual = first.Equals(second);

            // Assert
            isEqual.Should().BeFalse();
        }

        [Test]
        public void Default_TableAndKey_AreEmptyAndEqualToEmptyKey()
        {
            // Act
            var key = default(TextKey);

            // Assert
            key.Table.Should().BeEmpty();
            key.Key.Should().BeEmpty();
            key.Equals(new TextKey(string.Empty, string.Empty)).Should().BeTrue();
        }

        [Test]
        public void ToString_Key_ReturnsTableSlashKey()
        {
            // Act
            var text = new TextKey("Shared", "play").ToString();

            // Assert
            text.Should().Be("Shared/play");
        }
    }
}
