using Core.Domains;

namespace Core.Tests.Domains
{
    internal sealed class TestSubDomain
    {
        public ScopeRef LauncherScope { get; }
        public SecondTestDomainDescriptor Descriptor { get; }

        public TestSubDomain(ScopeRef launcherScope, SecondTestDomainDescriptor descriptor)
        {
            LauncherScope = launcherScope;
            Descriptor = descriptor;
        }
    }
}
