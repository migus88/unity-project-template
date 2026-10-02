using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Cheats;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

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
            await WaitForCheatAsync(ProbeCheat.CheatName);
            var reply = await _registry.ExecuteAsync(ProbeCheat.CheatName, CancellationToken.None);
            child.Dispose();

            // Assert
            reply.Should().Be("probed");
            _registry.Find(ProbeCheat.CheatName).Should().BeNull();
        }

        [Test]
        public async Task RegisterCheats_Provider_AddsEveryCheatUntilTheScopeIsDisposed()
        {
            // Arrange
            var child = _root.CreateScope(builder => builder.RegisterCheats<ProbeCheatProvider>());

            // Act
            await WaitForCheatAsync("beta");
            var names = _registry.Cheats.Select(cheat => cheat.Name).ToArray();
            child.Dispose();

            // Assert
            names.Should().Equal("alpha", "beta", CheatRegistry.HelpName);
            _registry.Cheats.Select(cheat => cheat.Name).Should().Equal(CheatRegistry.HelpName);
        }

        private async Task WaitForCheatAsync(string name)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UniTask.WaitUntil(() => _registry.Find(name) != null, cancellationToken: timeout.Token);
        }
    }
}
