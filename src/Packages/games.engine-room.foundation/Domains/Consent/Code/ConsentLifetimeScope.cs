using Consent.Prompt;
using Core.Audio;
using Core.Domains;
using Core.Localization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Consent
{
    internal sealed class ConsentLifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private LocalizationTable _text = null!;
        [SerializeField] private ConsentPromptView _view = null!;
        [SerializeField] private UiInteractionRelay _uiSounds = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<ConsentPromptPresenter>().AsSelf();
            builder.RegisterEntryPoint<ConsentCancelInputHandler>();
            builder.RegisterUiInteractionSounds(_uiSounds);
        }
    }
}
