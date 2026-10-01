using System.Threading;
using Cysharp.Threading.Tasks;

namespace Bootstrap
{
    public interface IMainFlow
    {
        UniTask RunAsync(CancellationToken ct);
    }
}
