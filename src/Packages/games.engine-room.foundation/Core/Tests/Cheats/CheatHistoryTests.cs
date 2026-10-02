using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatHistoryTests
    {
        private CheatHistory _history = null!;

        [SetUp]
        public void SetUp()
        {
            _history = new CheatHistory();
        }

        [Test]
        public void Previous_Empty_ReturnsNull()
        {
            // Act
            var line = _history.Previous("draft");

            // Assert
            line.Should().BeNull();
        }

        [Test]
        public void PreviousAndNext_Entries_WalkBackAndForthThenRestoreTheDraft()
        {
            // Arrange
            _history.Add("gold 1");
            _history.Add("heal");

            // Act
            var newest = _history.Previous("draft");
            var oldest = _history.Previous(newest!);
            var stillOldest = _history.Previous(oldest!);
            var back = _history.Next();
            var draft = _history.Next();
            var afterDraft = _history.Next();

            // Assert
            newest.Should().Be("heal");
            oldest.Should().Be("gold 1");
            stillOldest.Should().Be("gold 1");
            back.Should().Be("heal");
            draft.Should().Be("draft");
            afterDraft.Should().BeNull();
        }

        [Test]
        public void Add_ConsecutiveDuplicate_IsSkipped()
        {
            // Arrange
            _history.Add("heal");

            // Act
            _history.Add("heal ");

            // Assert
            _history.Count.Should().Be(1);
        }

        [Test]
        public void Add_PastCapacity_DropsTheOldest()
        {
            // Arrange
            for (var i = 0; i <= CheatHistory.Capacity; i++)
            {
                _history.Add($"gold {i}");
            }

            // Act
            string? oldest = null;

            for (var i = 0; i < CheatHistory.Capacity; i++)
            {
                oldest = _history.Previous(string.Empty);
            }

            // Assert
            _history.Count.Should().Be(CheatHistory.Capacity);
            oldest.Should().Be("gold 1");
        }
    }
}
