using Core.Domains;
using VContainer;
using VContainer.Unity;

namespace TestUtils
{
    public sealed class UnresolvableDomainScope : DomainLifetimeScope
    {
        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<EntryPointWithMissingDependency>();
        }

        private sealed class MissingDependency
        {
        }

        private sealed class EntryPointWithMissingDependency : IStartable
        {
            public EntryPointWithMissingDependency(MissingDependency dependency)
            {
            }

            public void Start()
            {
            }
        }
    }
}
