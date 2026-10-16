using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Cheats;
using NUnit.Framework;
using VContainer;
using VContainer.Unity;

namespace Core.Tests.Cheats
{
    public sealed class CheatRegistrationTests
    {
        private IObjectResolver _root = null!;
        private CheatRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<CheatRegistry>(Lifetime.Singleton);
            _root = builder.Build();
            _registry = _root.Resolve<CheatRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            _root.Dispose();
        }

        [Test]
        public async Task RegisterCheat_ChildScope_AddsTheCheatWhileTheScopeLives()
        {
            // Arrange
            var child = _root.CreateScope(builder => builder.RegisterCheat<ProbeCheat>());

            // Act
            StartEntryPoints(child);
            var reply = await _registry.ExecuteAsync(ProbeCheat.CheatName, CancellationToken.None);
            child.Dispose();

            // Assert
            reply.Should().Be("probed");
            _registry.Find(ProbeCheat.CheatName).Should().BeNull();
        }

        [Test]
        public void RegisterCheats_Provider_AddsEveryCheatUntilTheScopeIsDisposed()
        {
            // Arrange
            var child = _root.CreateScope(builder => builder.RegisterCheats<ProbeCheatProvider>());

            // Act
            StartEntryPoints(child);
            var names = _registry.Cheats.Select(cheat => cheat.Name).ToArray();
            child.Dispose();

            // Assert
            names.Should().Equal("alpha", "beta", CheatRegistry.HelpName);
            _registry.Cheats.Select(cheat => cheat.Name).Should().Equal(CheatRegistry.HelpName);
        }

        private static void StartEntryPoints(IObjectResolver scope)
        {
            foreach (var startable in scope.Resolve<IEnumerable<IStartable>>())
            {
                startable.Start();
            }
        }
    }
}
