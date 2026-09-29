using Core.Domains;
using Core.Localization;
using Settings;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainMenu
{
    internal sealed class MainMenuLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private LocalizationTable _text = null!;
        [SerializeField, Required] private MainMenuView _view = null!;
        [SerializeField, Required] private SettingsDomainDescriptor _settingsDescriptor = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<MainMenuPresenter>();
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
        }
    }
}
