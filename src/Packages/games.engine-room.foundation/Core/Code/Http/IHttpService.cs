using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Http
{
    public interface IHttpService
    {
        UniTask<HttpResult> SendAsync(HttpRequest request, CancellationToken ct);
    }
}
