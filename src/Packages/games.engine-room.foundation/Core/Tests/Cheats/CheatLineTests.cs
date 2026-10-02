using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatLineTests
    {
        [Test]
        public void Parse_Whitespace_SplitsTokensAndLowerCasesName()
        {
            // Act
            var line = CheatLine.Parse("  Gold   50\tnow ");

            // Assert
            line.Name.Should().Be("gold");
            line.Tokens.Should().HaveCount(3);
            line.Tokens[1].Value.Should().Be("50");
            line.Tokens[2].Value.Should().Be("now");
            line.EndsWithSpace.Should().BeTrue();
        }

        [Test]
        public void Parse_QuotedToken_KeepsItWhole()
        {
            // Act
            var line = CheatLine.Parse("card \"quick shot\" 2");

            // Assert
            line.Tokens.Should().HaveCount(3);
            line.Tokens[1].Value.Should().Be("quick shot");
            line.Tokens[1].IsQuoted.Should().BeTrue();
            line.Tokens[1].Start.Should().Be(5);
            line.Tokens[2].Value.Should().Be("2");
        }

        [Test]
        public void Parse_UnclosedQuote_RunsToTheEnd()
        {
            // Act
            var line = CheatLine.Parse("card \"quick sh");

            // Assert
            line.Tokens[1].Value.Should().Be("quick sh");
            line.EndsWithSpace.Should().BeFalse();
        }

        [Test]
        public void Parse_Backquote_IsStripped()
        {
            // Act
            var line = CheatLine.Parse("`help`");

            // Assert
            line.Text.Should().Be("help");
            line.Name.Should().Be("help");
        }

        [Test]
        public void Parse_NoTrailingSpace_ReportsIt()
        {
            // Act
            var line = CheatLine.Parse("gold 5");

            // Assert
            line.EndsWithSpace.Should().BeFalse();
            line.Tokens[1].Start.Should().Be(5);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Parse_EmptyLine_IsEmpty(string? text)
        {
            // Act
            var line = CheatLine.Parse(text);

            // Assert
            line.IsEmpty.Should().BeTrue();
            line.Name.Should().BeEmpty();
        }

        [Test]
        public void JoinValues_FromToken_JoinsTheRestWithSpaces()
        {
            // Arrange
            var line = CheatLine.Parse("card quick   shot");

            // Act
            var rest = line.JoinValues(1);

            // Assert
            rest.Should().Be("quick shot");
        }
    }
}
