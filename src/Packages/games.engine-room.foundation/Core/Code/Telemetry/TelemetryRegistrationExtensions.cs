using Core.Analytics;
using Core.Environment;
using VContainer;
using VContainer.Unity;

namespace Core.Telemetry
{
    public static class TelemetryRegistrationExtensions
    {
        public static void RegisterTelemetryBackend(this IContainerBuilder builder)
        {
            builder.Register(CreateOptions, Lifetime.Singleton);
            builder.Register<TelemetryQueueStore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TelemetryPipeline>().As<IAnalyticsBackend>();
        }

        private static TelemetryOptions CreateOptions(IObjectResolver resolver)
        {
            resolver.TryResolve<BuildEnvironment>(out var environment);
            var application = resolver.Resolve<IApplicationService>();
            return TelemetryOptions.From(environment, application.Version, application.Platform.ToString());
        }
    }
}
