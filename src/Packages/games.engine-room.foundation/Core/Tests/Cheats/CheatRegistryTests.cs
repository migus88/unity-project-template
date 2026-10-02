using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;

namespace Core.Tests.Cheats
{
    public sealed class CheatRegistryTests
    {
        private CheatRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new CheatRegistry();
        }

        [Test]
        public void Cheats_New_HoldsOnlyHelp()
        {
            // Act
            var names = _registry.Cheats.Select(cheat => cheat.Name);

            // Assert
            names.Should().Equal(CheatRegistry.HelpName);
        }

        [Test]
        public void Add_Cheat_KeepsNamesSortedUntilTheHandleIsDisposed()
        {
            // Arrange
            var handle = _registry.Add(new RecordingCheat("zap"));
            _registry.Add(new RecordingCheat("gold"));

            // Act
            var before = _registry.Cheats.Select(cheat => cheat.Name).ToArray();
            handle.Dispose();

            // Assert
            before.Should().Equal("gold", "help", "zap");
            _registry.Cheats.Select(cheat => cheat.Name).Should().Equal("gold", "help");
        }

        [Test]
        public void Add_DuplicateName_Throws()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));

            // Act
            Action act = () => _registry.Add(new RecordingCheat("gold"));

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Add_HelpName_Throws()
        {
            // Act
            Action act = () => _registry.Add(new RecordingCheat(CheatRegistry.HelpName));

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [TestCase("Gold")]
        [TestCase("give gold")]
        [TestCase("")]
        public void Add_NameNotOneLowerCaseWord_Throws(string name)
        {
            // Act
            Action act = () => _registry.Add(new RecordingCheat(name));

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Add_RequiredAfterOptional_Throws()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", CheatParameter.Int("times").Optional(), CheatParameter.Int("amount"));

            // Act
            Action act = () => _registry.Add(cheat);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task ExecuteAsync_ValidLine_RunsTheCheatWithParsedArguments()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", "added", CheatParameter.Int("amount"));
            _registry.Add(cheat);

            // Act
            var reply = await _registry.ExecuteAsync("GOLD 40", CancellationToken.None);

            // Assert
            reply.Should().Be("added");
            cheat.LastArguments!.GetInt("amount").Should().Be(40);
        }

        [Test]
        public async Task ExecuteAsync_InvalidArguments_RepliesWithoutRunningTheCheat()
        {
            // Arrange
            var cheat = new RecordingCheat("gold", CheatParameter.Int("amount"));
            _registry.Add(cheat);

            // Act
            var reply = await _registry.ExecuteAsync("gold lots", CancellationToken.None);

            // Assert
            reply.Should().Be("'lots' is not a whole number for <amount>.");
            cheat.Calls.Should().Be(0);
        }

        [Test]
        public async Task ExecuteAsync_UnknownName_SuggestsSimilarCheats()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("goldrush"));
            _registry.Add(new RecordingCheat("heal"));

            // Act
            var reply = await _registry.ExecuteAsync("gol", CancellationToken.None);

            // Assert
            reply.Should().Be("Unknown cheat 'gol'. Did you mean: gold, goldrush? Type help.");
        }

        [Test]
        public async Task ExecuteAsync_UnknownNameWithoutLookalikes_PointsToHelp()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));

            // Act
            var reply = await _registry.ExecuteAsync("teleport", CancellationToken.None);

            // Assert
            reply.Should().Be("Unknown cheat 'teleport'. Type help.");
        }

        [Test]
        public async Task ExecuteAsync_Help_ListsOneLinePerCheat()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold", CheatParameter.Int("amount")));
            _registry.Add(new RecordingCheat("heal"));

            // Act
            var reply = await _registry.ExecuteAsync("help", CancellationToken.None);

            // Assert
            reply.Split('\n').Should().Equal(
                "gold\t<amount>\tDescription of gold.",
                "heal\t\tDescription of heal.",
                "help\t[cheat]\tLists the cheats, or describes one.");
        }

        [Test]
        public async Task ExecuteAsync_HelpForOneCheat_DescribesItsParameters()
        {
            // Arrange
            _registry.Add(new RecordingCheat("mode", CheatParameter.Enum<TestMode>("mode"), CheatParameter.Int("times").Optional()));

            // Act
            var reply = await _registry.ExecuteAsync("help mode", CancellationToken.None);

            // Assert
            reply.Split('\n').Should().Equal(
                "Usage: mode <None|Easy|Hard> [times]",
                "Description of mode.",
                "<mode>\tNone, Easy, Hard",
                "[times]\twhole number (optional)");
        }

        [Test]
        public async Task ExecuteAsync_EmptyLine_PointsToHelp()
        {
            // Act
            var reply = await _registry.ExecuteAsync("  ", CancellationToken.None);

            // Assert
            reply.Should().Be("Type help to list the cheats.");
        }
    }
}
