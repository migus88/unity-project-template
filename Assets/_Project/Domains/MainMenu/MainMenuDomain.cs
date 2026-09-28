using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace MainMenu
{
    public sealed class MainMenuDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly MainMenuDomainDescriptor _descriptor;

        public MainMenuDomain(DomainRunner runner, ScopeRef launcherScope, MainMenuDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<MainMenuResult> RunAsync(MainMenuArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<MainMenuArgs, MainMenuResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(MainMenuArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
