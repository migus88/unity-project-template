using System.Threading;
using Core.Domains;
using Core.Privacy;
using Core.RemoteConfig;
using Cysharp.Threading.Tasks;

namespace Consent
{
    public sealed class ConsentDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        public bool IsPromptNeeded => _consent.NeedsPrompt(_policy.CurrentVersion(_remoteConfig));

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly ConsentDomainDescriptor _descriptor;
        private readonly IConsentService _consent;
        private readonly PrivacyPolicy _policy;
        private readonly IRemoteConfigService _remoteConfig;

        public ConsentDomain(DomainRunner runner, ScopeRef launcherScope, ConsentDomainDescriptor descriptor, IConsentService consent, PrivacyPolicy policy, IRemoteConfigService remoteConfig)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
            _consent = consent;
            _policy = policy;
            _remoteConfig = remoteConfig;
        }

        public UniTask<ConsentResult> RunAsync(ConsentArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<ConsentArgs, ConsentResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(ConsentArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
