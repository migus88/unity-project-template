using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Http
{
    public sealed class HttpService : IHttpService
    {
        private readonly IHttpTransport _transport;

        internal HttpService(IHttpTransport transport)
        {
            _transport = transport;
        }

        public async UniTask<HttpResult> SendAsync(HttpRequest request, CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timer = timeoutCts.CancelAfterSlim(request.Timeout, DelayType.Realtime);

            try
            {
                var response = await _transport.SendAsync(request, timeoutCts.Token);
                return HttpResultMapping.ToResult(response);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                return new HttpResult.Timeout(request.Timeout);
            }
        }
    }
}
