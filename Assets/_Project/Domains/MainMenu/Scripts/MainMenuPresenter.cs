using System;
using System.Threading;
using Core;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Cysharp.Threading.Tasks;
using R3;
using Settings;
using VContainer.Unity;

namespace MainMenu
{
    internal sealed class MainMenuPresenter : IStartable, IDisposable
    {
        private readonly MainMenuView _view;
        private readonly IApplicationService _application;
        private readonly ILocalizationService _localization;
        private readonly IInputService _input;
        private readonly SettingsDomain _settingsDomain;
        private readonly DomainCompletion<MainMenuResult> _completion;

        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;

        public MainMenuPresenter(MainMenuView view, IApplicationService application, ILocalizationService localization, IInputService input, SettingsDomain settingsDomain, DomainCompletion<MainMenuResult> completion)
        {
            _view = view;
            _application = application;
            _localization = localization;
            _input = input;
            _settingsDomain = settingsDomain;
            _completion = completion;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);

            _localization.Current.Subscribe(_ => _view.SetVersion(_localization.Format(SharedText.Version, _application.Version))).AddTo(ref _subscriptions);
            _view.PlayClicked.Subscribe(_ => Complete(new MainMenuResult.Play())).AddTo(ref _subscriptions);
            _view.QuitClicked.Subscribe(_ => Complete(new MainMenuResult.Quit())).AddTo(ref _subscriptions);
            _view.SettingsClicked
                .SubscribeAwait((_, ct) => OpenSettingsAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private void Complete(MainMenuResult result)
        {
            _view.SetInteractable(false);
            _completion.Complete(result);
        }

        private async UniTask OpenSettingsAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);
            await _settingsDomain.RunAsync(new SettingsArgs(), Transition.None, ct);
            _view.SetInteractable(true);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
