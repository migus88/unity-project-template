using System;
using Core.Domains;
using VContainer;
using VContainer.Unity;

namespace TestUtils
{
    public sealed class CancellingDomainScope : DomainLifetimeScope
    {
        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CancellingEntryPoint>();
        }

        private sealed class CancellingEntryPoint : IInitializable
        {
            public void Initialize()
            {
                throw new OperationCanceledException();
            }
        }
    }
}
