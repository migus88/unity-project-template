using Core.Domains;
using Core.Localization;
using Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainMenu
{
    internal sealed class MainMenuLifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private LocalizationTable _text = null!;
        [SerializeField] private MainMenuView _view = null!;
        [SerializeField] private SettingsDomainDescriptor _settingsDescriptor = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<MainMenuPresenter>();
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
        }
    }
}
