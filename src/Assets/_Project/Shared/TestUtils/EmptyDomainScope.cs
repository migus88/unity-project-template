using Core.Domains;
using VContainer;

namespace TestUtils
{
    public sealed class EmptyDomainScope : DomainLifetimeScope
    {
        protected override void ConfigureDomain(IContainerBuilder builder)
        {
        }
    }
}
