using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Http
{
    internal interface IHttpTransport
    {
        UniTask<HttpTransportResponse> SendAsync(HttpRequest request, CancellationToken ct);
    }
}
