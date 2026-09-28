#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace Core.Domains
{
    public interface IDebugRunnableDomain
    {
        DomainDescriptor Descriptor { get; }

#if UNITY_EDITOR
        UniTask<object> RunDebugAsync(CancellationToken ct);
#endif
    }
}
