using System;
using System.Threading;
using Core;
using Core.Analytics;
using Core.Audio;
using Core.Domains;
using Core.Input;
using Core.Privacy;
using Core.RemoteConfig;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace Consent.Prompt
{
    internal sealed class ConsentPromptPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;
        private bool _isChoosing;

        private readonly ConsentPromptView _view;
        private readonly IConsentService _consent;
        private readonly PrivacyPolicy _policy;
        private readonly IRemoteConfigService _remoteConfig;
        private readonly IApplicationService _application;
        private readonly IInputService _input;
        private readonly IAnalytics _analytics;
        private readonly IUiInteractionSounds _uiSounds;
        private readonly DomainCompletion<ConsentResult> _completion;
        private readonly Subject<bool> _choices = new();

        public ConsentPromptPresenter(ConsentPromptView view, IConsentService consent, PrivacyPolicy policy, IRemoteConfigService remoteConfig, IApplicationService application, IInputService input, IAnalytics analytics, IUiInteractionSounds uiSounds, DomainCompletion<ConsentResult> completion)
        {
            _view = view;
            _consent = consent;
            _policy = policy;
            _remoteConfig = remoteConfig;
            _application = application;
            _input = input;
            _analytics = analytics;
            _uiSounds = uiSounds;
            _completion = completion;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);
            _view.SetPolicyLinkVisible(_policy.Url != null);

            _view.YesClicked.Subscribe(_ => _choices.OnNext(true)).AddTo(ref _subscriptions);
            _view.NoClicked.Merge(_view.CloseClicked).Subscribe(_ => _choices.OnNext(false)).AddTo(ref _subscriptions);
            _view.PolicyClicked.Subscribe(_ => OpenPolicy()).AddTo(ref _subscriptions);
            _choices
                .SubscribeAwait((isGranted, ct) => ChooseAsync(isGranted, ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        public void Decline()
        {
            if (_completion.IsCompleted || _isChoosing)
            {
                return;
            }

            _uiSounds.Play(UiInteraction.Back);
            _choices.OnNext(false);
        }

        private async UniTask ChooseAsync(bool isGranted, CancellationToken ct)
        {
            if (_completion.IsCompleted || _isChoosing)
            {
                return;
            }

            _isChoosing = true;
            _view.SetInteractable(false);
            var policyVersion = _policy.CurrentVersion(_remoteConfig);

            if (isGranted)
            {
                await _consent.GrantAsync(policyVersion, ct);
                _analytics.Track(new ConsentGrantedEvent(policyVersion));
                _completion.Complete(new ConsentResult.Granted());
                return;
            }

            await _consent.DenyAsync(policyVersion, ct);
            _completion.Complete(new ConsentResult.Denied());
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
            _choices.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
