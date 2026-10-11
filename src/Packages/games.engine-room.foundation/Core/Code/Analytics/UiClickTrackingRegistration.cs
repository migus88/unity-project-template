using VContainer;
using VContainer.Unity;

namespace Core.Analytics
{
    public static class UiClickTrackingRegistration
    {
        public static void RegisterUiClickTracking(this IContainerBuilder builder, UiClickRelay relay)
        {
            builder.RegisterComponent(relay);
            builder.RegisterEntryPoint<UiClickTrackingPresenter>();
        }
    }
}
