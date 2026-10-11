using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Http;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Http
{
    public sealed class HttpServiceTests
    {
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

        [Test]
        public async Task SendAsync_TransportOutlastsTimeout_Timeout()
        {
            // Arrange
            var service = new HttpService(new HangingTransport());
            var request = HttpRequest.Get("https://example.test/config", ShortTimeout);

            // Act
            var result = await service.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().BeCase<HttpResult.Timeout>().Which.After.Should().Be(ShortTimeout);
        }

        [Test]
        public async Task SendAsync_CallerCancels_ThrowsOperationCanceled()
        {
            // Arrange
            var service = new HttpService(new HangingTransport());
            var request = HttpRequest.Get("https://example.test/config", TimeSpan.FromMinutes(1));
            using var cts = new CancellationTokenSource();
            var send = service.SendAsync(request, cts.Token);

            // Act
            cts.Cancel();
            Func<Task> act = async () => await send;

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task SendAsync_TransportResponds_PassesRequestAndMapsResponse()
        {
            // Arrange
            var transport = new RespondingTransport(new HttpTransportResponse(HttpTransportOutcome.Success, 200, Array.Empty<byte>(), new Dictionary<string, string>(), string.Empty));
            var service = new HttpService(transport);
            var request = HttpRequest.PostJson("https://example.test/events", "{}", false, TimeSpan.FromSeconds(5));

            // Act
            var result = await service.SendAsync(request, CancellationToken.None);

            // Assert
            transport.Received.Should().BeSameAs(request);
            result.Should().BeCase<HttpResult.Completed>();
        }

        private sealed class HangingTransport : IHttpTransport
        {
            public async UniTask<HttpTransportResponse> SendAsync(HttpRequest request, CancellationToken ct)
            {
                await UniTask.Never(ct);
                throw new InvalidOperationException("Never completes.");
            }
        }

        private sealed class RespondingTransport : IHttpTransport
        {
            public HttpRequest? Received { get; private set; }

            private readonly HttpTransportResponse _response;

            public RespondingTransport(HttpTransportResponse response)
            {
                _response = response;
            }

            public UniTask<HttpTransportResponse> SendAsync(HttpRequest request, CancellationToken ct)
            {
                Received = request;
                return UniTask.FromResult(_response);
            }
        }
    }
}
