using Core.Domains;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Settings
{
    internal sealed class SettingsLifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private SettingsView _view = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<SettingsPresenter>();
        }
    }
}
