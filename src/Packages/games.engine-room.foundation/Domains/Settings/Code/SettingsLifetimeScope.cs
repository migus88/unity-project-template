using Core.Audio;
using Core.Domains;
using Core.Localization;
using Settings.Privacy;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Settings
{
    internal sealed class SettingsLifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private LocalizationTable _text = null!;
        [SerializeField] private SettingsView _view = null!;
        [SerializeField] private PrivacyRowView _privacy = null!;
        [SerializeField] private UiInteractionRelay _uiSounds = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<SettingsPresenter>();
            builder.RegisterComponent(_privacy);
            builder.RegisterEntryPoint<PrivacyPresenter>();
            builder.RegisterUiInteractionSounds(_uiSounds);
        }
    }
}
