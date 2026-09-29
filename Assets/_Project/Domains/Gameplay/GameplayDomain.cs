using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Gameplay
{
    public sealed class GameplayDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly GameplayDomainDescriptor _descriptor;

        public GameplayDomain(DomainRunner runner, ScopeRef launcherScope, GameplayDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<GameplayResult> RunAsync(GameplayArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<GameplayArgs, GameplayResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(GameplayArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
