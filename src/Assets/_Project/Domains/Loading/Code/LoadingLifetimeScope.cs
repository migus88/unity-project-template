using Core.Domains;
using Core.Localization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Loading
{
    internal sealed class LoadingLifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private LocalizationTable _text = null!;
        [SerializeField] private LoadingScreenView _view = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<LoadingScreenPresenter>();
        }
    }
}
