using System;
using System.Collections.Generic;
using System.Threading;
using Core;
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
        private readonly SettingsView _view;
        private readonly ISettingsService _settings;
        private readonly IInputService _input;
        private readonly CoreConfig _coreConfig;
        private readonly DomainCompletion<SettingsResult> _completion;

        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;

        public SettingsPresenter(SettingsView view, ISettingsService settings, IInputService input, CoreConfig coreConfig, DomainCompletion<SettingsResult> completion)
        {
            _view = view;
            _settings = settings;
            _input = input;
            _coreConfig = coreConfig;
            _completion = completion;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);

            var state = _settings.Current.CurrentValue;
            var languages = _coreConfig.SupportedLanguages;
            _view.SetVolumes(state.MasterVolume, state.MusicVolume, state.SfxVolume, state.UiVolume);
            _view.SetLanguages(GetNativeNames(languages), Array.IndexOf(languages, state.Language));

            _view.MasterVolumeChanged.Subscribe(volume => Apply(current => current with { MasterVolume = volume })).AddTo(ref _subscriptions);
            _view.MusicVolumeChanged.Subscribe(volume => Apply(current => current with { MusicVolume = volume })).AddTo(ref _subscriptions);
            _view.SfxVolumeChanged.Subscribe(volume => Apply(current => current with { SfxVolume = volume })).AddTo(ref _subscriptions);
            _view.UiVolumeChanged.Subscribe(volume => Apply(current => current with { UiVolume = volume })).AddTo(ref _subscriptions);
            _view.LanguageIndexChanged.Subscribe(index => Apply(current => current with { Language = languages[index] })).AddTo(ref _subscriptions);
            _view.BackClicked
                .SubscribeAwait((_, ct) => CloseAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private void Apply(Func<SettingsState, SettingsState> change)
        {
            _settings.Apply(change(_settings.Current.CurrentValue));
        }

        private async UniTask CloseAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);

            var saved = await _settings.SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Settings, $"Settings could not be saved: {error.Message}");
            }

            _completion.Complete(new SettingsResult.Closed());
        }

        private static List<string> GetNativeNames(IReadOnlyList<Language> languages)
        {
            var names = new List<string>(languages.Count);

            foreach (var language in languages)
            {
                names.Add(language.GetNativeName());
            }

            return names;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
