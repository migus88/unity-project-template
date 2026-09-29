using VContainer;

namespace Core.Domains
{
    public static class DomainRegistrationExtensions
    {
        public static void RegisterDomain<TDomain>(this IContainerBuilder builder, DomainDescriptor descriptor)
            where TDomain : class, IDebugRunnableDomain
        {
            builder.RegisterInstance(descriptor, descriptor.GetType());
            builder.Register<TDomain>(Lifetime.Singleton).AsSelf().As<IDebugRunnableDomain>();
        }
    }
}
