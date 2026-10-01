using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Settings
{
    public sealed class SettingsDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly SettingsDomainDescriptor _descriptor;

        public SettingsDomain(DomainRunner runner, ScopeRef launcherScope, SettingsDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<SettingsResult> RunAsync(SettingsArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<SettingsArgs, SettingsResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(SettingsArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
