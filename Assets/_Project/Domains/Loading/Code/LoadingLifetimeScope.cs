using Core.Domains;
using Core.Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Loading
{
    internal sealed class LoadingLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private LocalizationTable _text = null!;
        [SerializeField, Required] private LoadingScreenView _view = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<LoadingScreenPresenter>();
        }
    }
}
