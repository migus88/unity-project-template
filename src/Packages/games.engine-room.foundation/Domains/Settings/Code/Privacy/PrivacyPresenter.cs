using System;
using System.Threading;
using Core;
using Core.Analytics;
using Core.Localization;
using Core.Privacy;
using Core.RemoteConfig;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace Settings.Privacy
{
    internal sealed class PrivacyPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly PrivacyRowView _view;
        private readonly IConsentService _consent;
        private readonly PrivacyPolicy _policy;
        private readonly IApplicationService _application;
        private readonly ILocalizationService _localization;
        private readonly IAnalytics _analytics;
        private readonly IRemoteConfigService _remoteConfig;

        public PrivacyPresenter(PrivacyRowView view, IConsentService consent, PrivacyPolicy policy, IApplicationService application, ILocalizationService localization, IAnalytics analytics, IRemoteConfigService remoteConfig)
        {
            _view = view;
            _consent = consent;
            _policy = policy;
            _application = application;
            _localization = localization;
            _analytics = analytics;
            _remoteConfig = remoteConfig;
        }

        public void Start()
        {
            _view.SetPolicyLinkVisible(_policy.Url != null);

            Observable.CombineLatest(_consent.Current, _localization.Current, (snapshot, _) => snapshot)
                .Subscribe(Render)
                .AddTo(ref _subscriptions);
            _view.ConsentToggled
                .SubscribeAwait((_, ct) => ToggleAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
            _view.PolicyClicked.Subscribe(_ => OpenPolicy()).AddTo(ref _subscriptions);
        }

        private void Render(ConsentSnapshot snapshot)
        {
            var isGranted = snapshot.State == ConsentState.Granted;
            _view.SetConsent(_localization.Get(isGranted ? SettingsText.Yes : SettingsText.No));

            var hasId = snapshot.InstallId != null;
            _view.SetInstallId(snapshot.InstallId ?? _localization.Get(SettingsText.InstallIdNone), hasId);
        }

        private async UniTask ToggleAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);

            var policyVersion = _policy.CurrentVersion(_remoteConfig);

            if (_consent.IsGranted)
            {
                await _consent.DenyAsync(policyVersion, ct);
            }
            else
            {
                await _consent.GrantAsync(policyVersion, ct);
            }

            _analytics.Track(new ConsentChangedEvent(_consent.IsGranted));
            _view.SetInteractable(true);
        }

        private void OpenPolicy()
        {
            if (_policy.Url != null)
            {
                _application.OpenUrl(_policy.Url);
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
