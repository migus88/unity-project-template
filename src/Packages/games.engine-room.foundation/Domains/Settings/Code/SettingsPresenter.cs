using System;
using System.Threading;
using Core;
using Core.Analytics;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Core.Logging;
using Core.Settings;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace Settings
{
    internal sealed class SettingsPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;
        private int _languageIndex;
        private SettingsState? _opened;

        private readonly SettingsView _view;
        private readonly ISettingsService _settings;
        private readonly IInputService _input;
        private readonly CoreConfig _coreConfig;
        private readonly DomainCompletion<SettingsResult> _completion;
        private readonly IAnalytics _analytics;

        public SettingsPresenter(SettingsView view, ISettingsService settings, IInputService input, CoreConfig coreConfig, DomainCompletion<SettingsResult> completion, IAnalytics analytics)
        {
            _view = view;
            _settings = settings;
            _input = input;
            _coreConfig = coreConfig;
            _completion = completion;
            _analytics = analytics;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);

            var state = _settings.Current.CurrentValue;
            _opened = state;
            _languageIndex = Array.IndexOf(_coreConfig.SupportedLanguages, state.Language);
            _view.SetVolumes(state.MasterVolume, state.MusicVolume, state.SfxVolume, state.UiVolume);
            _view.SetLanguage(state.Language.GetNativeName());

            _view.MasterVolumeChanged.Subscribe(volume => Apply(current => current with { MasterVolume = volume })).AddTo(ref _subscriptions);
            _view.MusicVolumeChanged.Subscribe(volume => Apply(current => current with { MusicVolume = volume })).AddTo(ref _subscriptions);
            _view.SfxVolumeChanged.Subscribe(volume => Apply(current => current with { SfxVolume = volume })).AddTo(ref _subscriptions);
            _view.UiVolumeChanged.Subscribe(volume => Apply(current => current with { UiVolume = volume })).AddTo(ref _subscriptions);
            _view.PreviousLanguageClicked.Subscribe(_ => SelectLanguage(-1)).AddTo(ref _subscriptions);
            _view.NextLanguageClicked.Subscribe(_ => SelectLanguage(1)).AddTo(ref _subscriptions);
            _view.BackClicked
                .SubscribeAwait((_, ct) => CloseAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private void Apply(Func<SettingsState, SettingsState> change)
        {
            _settings.Apply(change(_settings.Current.CurrentValue));
        }

        private void SelectLanguage(int step)
        {
            var languages = _coreConfig.SupportedLanguages;
            _languageIndex = (_languageIndex + step + languages.Length) % languages.Length;
            var language = languages[_languageIndex];
            Apply(current => current with { Language = language });
            _view.SetLanguage(language.GetNativeName());
        }

        private async UniTask CloseAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);

            var saved = await _settings.SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Settings, $"Settings could not be saved: {error.Message}");
            }

            TrackChanges();
            _completion.Complete(new SettingsResult.Closed());
        }

        private void TrackChanges()
        {
            if (_opened == null)
            {
                return;
            }

            foreach (var change in SettingsChanges.Diff(_opened, _settings.Current.CurrentValue))
            {
                _analytics.Track(change);
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
