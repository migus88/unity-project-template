using VContainer;
using VContainer.Unity;

namespace Core.Audio
{
    public static class UiInteractionSoundRegistration
    {
        public static void RegisterUiInteractionSounds(this IContainerBuilder builder, UiInteractionRelay relay)
        {
            builder.RegisterComponent(relay);
            builder.RegisterEntryPoint<UiInteractionSoundPresenter>();
        }
    }
}
