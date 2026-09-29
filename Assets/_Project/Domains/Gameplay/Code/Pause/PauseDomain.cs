using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Gameplay.Pause
{
    internal sealed class PauseDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly PauseDomainDescriptor _descriptor;

        public PauseDomain(DomainRunner runner, ScopeRef launcherScope, PauseDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<PauseResult> RunAsync(PauseArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<PauseArgs, PauseResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(new PauseArgs(), Transition.None, ct);
        }
#endif
    }
}
