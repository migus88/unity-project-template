using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Loading
{
    public sealed class LoadingDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly LoadingDomainDescriptor _descriptor;

        public LoadingDomain(DomainRunner runner, ScopeRef launcherScope, LoadingDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<LoadingResult> RunAsync(LoadingArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<LoadingArgs, LoadingResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(LoadingArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
