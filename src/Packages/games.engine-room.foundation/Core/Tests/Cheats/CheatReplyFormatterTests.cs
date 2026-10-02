using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatReplyFormatterTests
    {
        [Test]
        public void ToRichText_PlainText_EscapesTags()
        {
            // Act
            var rich = CheatReplyFormatter.ToRichText("Usage: draw <b>");

            // Assert
            rich.Should().Be("<noparse>Usage: draw <b></noparse>");
        }

        [Test]
        public void ToRichText_TabColumns_AlignsEveryLineAtTheSamePositions()
        {
            // Act
            var rich = CheatReplyFormatter.ToRichText("gold\t<amount>\tAdds gold.\nhelp\t[cheat]\tLists.");

            // Assert
            var lines = rich.Split('\n');
            lines[0].Should().Be("<noparse>gold</noparse><pos=3.7em><noparse><amount></noparse><pos=9.6em><noparse>Adds gold.</noparse>");
            lines[1].Should().Be("<noparse>help</noparse><pos=3.7em><noparse>[cheat]</noparse><pos=9.6em><noparse>Lists.</noparse>");
        }

        [Test]
        public void ToRichText_EmptyCell_KeepsTheColumn()
        {
            // Act
            var rich = CheatReplyFormatter.ToRichText("heal\t\tHeals.");

            // Assert
            rich.Should().Be("<noparse>heal</noparse><pos=3.7em><pos=5.2em><noparse>Heals.</noparse>");
        }

        [Test]
        public void ToRichText_Empty_ReturnsEmpty()
        {
            // Act
            var rich = CheatReplyFormatter.ToRichText(string.Empty);

            // Assert
            rich.Should().BeEmpty();
        }
    }
}
