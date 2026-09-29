using System;
using Core.Domains;
using VContainer;
using VContainer.Unity;

namespace TestUtils
{
    public sealed class FailingDomainScope : DomainLifetimeScope
    {
        public const string FailureMessage = "The test entry point failed.";

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<FailingEntryPoint>();
        }

        private sealed class FailingEntryPoint : IInitializable
        {
            public void Initialize()
            {
                throw new InvalidOperationException(FailureMessage);
            }
        }
    }
}
