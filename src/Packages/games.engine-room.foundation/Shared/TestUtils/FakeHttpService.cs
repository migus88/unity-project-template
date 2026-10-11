using System;
using System.Collections.Generic;
using System.Threading;
using Core.Http;
using Cysharp.Threading.Tasks;

namespace TestUtils
{
    public sealed class FakeHttpService : IHttpService
    {
        public IReadOnlyList<HttpRequest> Requests => _requests;

        private Func<HttpRequest, HttpResult>? _responder;

        private readonly List<HttpRequest> _requests = new();
        private readonly Queue<HttpResult> _queued = new();

        public void Enqueue(HttpResult result)
        {
            _queued.Enqueue(result);
        }

        public void Respond(Func<HttpRequest, HttpResult> responder)
        {
            _responder = responder;
        }

        public UniTask<HttpResult> SendAsync(HttpRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _requests.Add(request);

            if (_queued.Count > 0)
            {
                return UniTask.FromResult(_queued.Dequeue());
            }

            if (_responder != null)
            {
                return UniTask.FromResult(_responder(request));
            }

            throw new InvalidOperationException($"No HTTP result queued for {request.Method} {request.Url}.");
        }
    }
}
