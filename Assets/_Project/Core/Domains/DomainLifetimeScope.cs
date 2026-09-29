using System;
using VContainer;
using VContainer.Unity;

namespace Core.Domains
{
    public abstract class DomainLifetimeScope : LifetimeScope
    {
        protected sealed override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                throw new InvalidOperationException($"{GetType().Name} has no parent scope. Domain scopes must be built by {nameof(DomainRunner)}.");
            }

            var parentDepth = Parent.Container.Resolve<ScopeRef>().Depth;
            builder.RegisterEntryPointFailureHandler();
            builder.RegisterInstance(new ScopeRef(this, parentDepth + 1));
            builder.Register<DomainSceneSet>(Lifetime.Singleton);
            ConfigureDomain(builder);
        }

        protected abstract void ConfigureDomain(IContainerBuilder builder);
    }
}
