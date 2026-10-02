using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatCompleterTests
    {
        private static readonly string[] Cards = { "Quick Shot", "Quick Step", "Slam" };

        private CheatRegistry _registry = null!;
        private CheatCompleter _completer = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new CheatRegistry();
            _registry.Add(new RecordingCheat("gold", CheatParameter.Int("amount")));
            _registry.Add(new RecordingCheat("god", CheatParameter.Bool("enabled")));
            _registry.Add(new RecordingCheat("mode", CheatParameter.Enum<TestMode>("mode")));
            _registry.Add(new RecordingCheat("card", CheatParameter.Choice("card", () => Cards)));
            _registry.Add(new RecordingCheat("spawn", CheatParameter.Text("unit", () => new[] { "Grunt", "Gunner" }), CheatParameter.Int("count").Optional()));
            _completer = new CheatCompleter(_registry);
        }

        [Test]
        public void Suggest_CommandPrefix_ReturnsMatchingNames()
        {
            // Act
            var suggestions = _completer.Suggest("go");

            // Assert
            suggestions.Should().Equal("god", "gold");
        }

        [Test]
        public void Suggest_EmptyLine_ReturnsNothing()
        {
            // Act
            var suggestions = _completer.Suggest(string.Empty);

            // Assert
            suggestions.Should().BeEmpty();
        }

        [Test]
        public void Suggest_NameAndSpace_ReturnsTheParameterValues()
        {
            // Act
            var suggestions = _completer.Suggest("mode ");

            // Assert
            suggestions.Should().Equal("None", "Easy", "Hard");
        }

        [Test]
        public void Suggest_BoolPrefix_ReturnsMatchingWords()
        {
            // Act
            var suggestions = _completer.Suggest("god o");

            // Assert
            suggestions.Should().Equal("on", "off");
        }

        [Test]
        public void Suggest_ProviderValuesAfterSpaceInsideRestOfLine_FiltersByTheWholeValue()
        {
            // Act
            var suggestions = _completer.Suggest("card quick s");

            // Assert
            suggestions.Should().Equal("Quick Shot", "Quick Step");
        }

        [Test]
        public void Suggest_IntParameter_ReturnsNothing()
        {
            // Act
            var suggestions = _completer.Suggest("gold ");

            // Assert
            suggestions.Should().BeEmpty();
        }

        [Test]
        public void Suggest_TextBeforeAnotherParameter_UsesItsSuggestions()
        {
            // Act
            var suggestions = _completer.Suggest("spawn g");

            // Assert
            suggestions.Should().Equal("Grunt", "Gunner");
        }

        [Test]
        public void Complete_SingleCandidate_CompletesItWithATrailingSpace()
        {
            // Act
            var completion = _completer.Complete("mo", 0);

            // Assert
            completion.Should().Be(new CheatCompletion("mode ", false));
        }

        [Test]
        public void Complete_SharedPrefix_ExtendsTheToken()
        {
            // Act
            var completion = _completer.Complete("card q", 0);

            // Assert
            completion.Should().Be(new CheatCompletion("card Quick S", false));
        }

        [Test]
        public void Complete_NoLongerPrefix_CyclesTheCandidates()
        {
            // Act
            var first = _completer.Complete("card Quick S", 0);
            var second = _completer.Complete("card Quick S", 1);
            var wrapped = _completer.Complete("card Quick S", 2);

            // Assert
            first.Should().Be(new CheatCompletion("card Quick Shot", true));
            second.Should().Be(new CheatCompletion("card Quick Step", true));
            wrapped.Should().Be(first);
        }

        [Test]
        public void Complete_ValueWithSpaceBeforeAnotherParameter_QuotesIt()
        {
            // Arrange
            _registry.Add(new RecordingCheat("give", CheatParameter.Choice("card", () => new[] { "Heavy Slam" }), CheatParameter.Int("count")));

            // Act
            var completion = _completer.Complete("give hea", 0);

            // Assert
            completion.Line.Should().Be("give \"Heavy Slam\" ");
        }

        [Test]
        public void Complete_NoCandidates_LeavesTheLine()
        {
            // Act
            var completion = _completer.Complete("gold 1", 0);

            // Assert
            completion.Should().Be(new CheatCompletion("gold 1", false));
        }
    }
}
