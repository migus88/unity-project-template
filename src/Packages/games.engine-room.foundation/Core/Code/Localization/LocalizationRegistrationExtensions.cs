using VContainer;
using VContainer.Unity;

namespace Core.Localization
{
    public static class LocalizationRegistrationExtensions
    {
        public static void RegisterLocalizationTable(this IContainerBuilder builder, LocalizationTable table)
        {
            builder.RegisterEntryPoint(resolver => new LocalizationTableRegistration(resolver.Resolve<LocalizationService>(), table), Lifetime.Singleton);
        }
    }
}
