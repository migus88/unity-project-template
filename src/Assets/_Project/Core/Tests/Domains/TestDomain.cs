using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Core.Tests.Domains
{
    internal sealed class TestDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;
        public ScopeRef LauncherScope { get; }

        private readonly FirstTestDomainDescriptor _descriptor;

        public TestDomain(ScopeRef launcherScope, FirstTestDomainDescriptor descriptor)
        {
            LauncherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return UniTask.FromResult<object>(new TestDomainResult(0));
        }
    }
}
