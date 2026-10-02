using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatValuesTests
    {
        private static readonly string[] Cards = { "Quick Shot", "Quick Step", "Heavy_Slam", "Slam" };

        [TestCase("quick shot", "Quick Shot")]
        [TestCase("QUICKSHOT", "Quick Shot")]
        [TestCase("heavyslam", "Heavy_Slam")]
        [TestCase("heavy", "Heavy_Slam")]
        [TestCase("slam", "Slam")]
        [TestCase("quick st", "Quick Step")]
        public void Match_KnownValue_ReturnsIt(string input, string expected)
        {
            // Act
            var match = CheatValues.Match(Cards, input);

            // Assert
            match.Should().Be(expected);
        }

        [TestCase("quick")]
        [TestCase("zzz")]
        [TestCase("")]
        [TestCase("   ")]
        public void Match_AmbiguousUnknownOrEmpty_ReturnsNull(string input)
        {
            // Act
            var match = CheatValues.Match(Cards, input);

            // Assert
            match.Should().BeNull();
        }

        [Test]
        public void Matches_SharedPrefix_ReturnsEveryCandidate()
        {
            // Act
            var matches = CheatValues.Matches(Cards, card => card, "quick");

            // Assert
            matches.Should().Equal("Quick Shot", "Quick Step");
        }

        [Test]
        public void Match_Items_ResolvesThroughTheNameSelector()
        {
            // Arrange
            var items = new[] { new Item("Alpha Unit"), new Item("Beta Unit") };

            // Act
            var match = CheatValues.Match(items, item => item.Name, "beta");

            // Assert
            match.Should().BeSameAs(items[1]);
        }

        private sealed record Item(string Name);
    }
}
