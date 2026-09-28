using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace Core.Tests.Domains
{
    internal sealed class TestDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly FirstTestDomainDescriptor _descriptor;

        public TestDomain(FirstTestDomainDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        public UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return UniTask.FromResult<object>(new TestDomainResult(0));
        }
    }
}
