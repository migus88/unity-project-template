using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Cheats
{
    public sealed class CheatArgumentParserTests
    {
        private static readonly string[] Cards = { "Quick Shot", "Quick Step", "Slam" };

        [Test]
        public void Parse_ValidInt_StoresIt()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", CheatParameter.Int("amount"));

            // Act
            var result = Parse(cheat, "gold -25");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetInt("amount").Should().Be(-25);
        }

        [Test]
        public void Parse_InvalidInt_ExplainsTheArgument()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", CheatParameter.Int("amount"));

            // Act
            var result = Parse(cheat, "gold abc");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("'abc' is not a whole number for <amount>.");
        }

        [Test]
        public void Parse_FloatWithDot_UsesInvariantCulture()
        {
            // Arrange
            var cheat = new RecordingCheat("speed", CheatParameter.Float("scale"));

            // Act
            var result = Parse(cheat, "speed 1.5");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetFloat("scale").Should().Be(1.5f);
        }

        [Test]
        public void Parse_InvalidFloat_ExplainsTheArgument()
        {
            // Arrange
            var cheat = new RecordingCheat("speed", CheatParameter.Float("scale"));

            // Act
            var result = Parse(cheat, "speed fast");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("'fast' is not a number for <scale>.");
        }

        [TestCase("on", true)]
        [TestCase("OFF", false)]
        [TestCase("yes", true)]
        [TestCase("0", false)]
        public void Parse_BoolWord_StoresIt(string word, bool expected)
        {
            // Arrange
            var cheat = new RecordingCheat("god", CheatParameter.Bool("enabled"));

            // Act
            var result = Parse(cheat, $"god {word}");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetBool("enabled").Should().Be(expected);
        }

        [Test]
        public void Parse_InvalidBool_ExplainsTheArgument()
        {
            // Arrange
            var cheat = new RecordingCheat("god", CheatParameter.Bool("enabled"));

            // Act
            var result = Parse(cheat, "god maybe");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("'maybe' is not on or off for <enabled>.");
        }

        [Test]
        public void Parse_EnumNameAnyCase_StoresTheMember()
        {
            // Arrange
            var cheat = new RecordingCheat("mode", CheatParameter.Enum<TestMode>("mode"));

            // Act
            var result = Parse(cheat, "mode HARD");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetEnum<TestMode>("mode").Should().Be(TestMode.Hard);
        }

        [Test]
        public void Parse_UnknownEnum_ListsTheValues()
        {
            // Arrange
            var cheat = new RecordingCheat("mode", CheatParameter.Enum<TestMode>("mode"));

            // Act
            var result = Parse(cheat, "mode extreme");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("No mode 'extreme'. Values: None, Easy, Hard.");
        }

        [Test]
        public void Parse_OptionalMissing_HasNoValue()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", new[] { CheatParameter.Int("amount"), CheatParameter.Int("times").Optional() });

            // Act
            var result = Parse(cheat, "gold 5");

            // Assert
            var arguments = result.Should().BeCase<CheatParse.Parsed>().Which.Arguments;
            arguments.HasValue("times").Should().BeFalse();
            arguments.GetInt("times", 1).Should().Be(1);
        }

        [Test]
        public void Parse_RequiredMissing_ShowsUsage()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", new[] { CheatParameter.Int("amount"), CheatParameter.Int("times").Optional() });

            // Act
            var result = Parse(cheat, "gold");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("Usage: gold <amount> [times]");
        }

        [Test]
        public void Parse_TooManyArguments_ShowsUsage()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", CheatParameter.Int("amount"));

            // Act
            var result = Parse(cheat, "gold 5 6");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("Too many arguments. Usage: gold <amount>");
        }

        [Test]
        public void Parse_LastText_TakesTheRestOfTheLine()
        {
            // Arrange
            var cheat = new RecordingCheat("say", new[] { CheatParameter.Int("times"), CheatParameter.Text("message") });

            // Act
            var result = Parse(cheat, "say 2 hello   there world");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetText("message").Should().Be("hello there world");
        }

        [Test]
        public void Parse_ChoiceExactWithSpaces_ResolvesIt()
        {
            // Arrange
            var cheat = new RecordingCheat("card", CheatParameter.Choice("card", () => Cards));

            // Act
            var result = Parse(cheat, "card quick shot");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetText("card").Should().Be("Quick Shot");
        }

        [Test]
        public void Parse_ChoiceUniquePrefix_ResolvesIt()
        {
            // Arrange
            var cheat = new RecordingCheat("card", CheatParameter.Choice("card", () => Cards));

            // Act
            var result = Parse(cheat, "card sl");

            // Assert
            result.Should().BeCase<CheatParse.Parsed>().Which.Arguments.GetText("card").Should().Be("Slam");
        }

        [Test]
        public void Parse_ChoiceAmbiguous_ListsTheCandidates()
        {
            // Arrange
            var cheat = new RecordingCheat("card", CheatParameter.Choice("card", () => Cards));

            // Act
            var result = Parse(cheat, "card quick");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("'quick' matches several card values: Quick Shot, Quick Step.");
        }

        [Test]
        public void Parse_ChoiceUnknown_ListsTheValues()
        {
            // Arrange
            var cheat = new RecordingCheat("card", CheatParameter.Choice("card", () => Cards));

            // Act
            var result = Parse(cheat, "card xyz");

            // Assert
            result.Should().BeCase<CheatParse.Invalid>().Which.Message.Should().Be("No card 'xyz'. Values: Quick Shot, Quick Step, Slam.");
        }

        private static CheatParse Parse(ICheat cheat, string text)
        {
            return CheatArgumentParser.Parse(cheat, CheatLine.Parse(text));
        }
    }
}
