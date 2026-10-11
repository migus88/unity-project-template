using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace Core.Http
{
    internal sealed class UnityWebRequestTransport : IHttpTransport
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

        public async UniTask<HttpTransportResponse> SendAsync(HttpRequest request, CancellationToken ct)
        {
            using var webRequest = new UnityWebRequest(request.Url, ToVerb(request.Method));
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            if (request.Body != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(request.Body) { contentType = request.ContentType };
            }

            foreach (var header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            try
            {
                await webRequest.SendWebRequest().WithCancellation(ct);
            }
            catch (UnityWebRequestException)
            {
            }

            return ToResponse(webRequest);
        }

        private static HttpTransportResponse ToResponse(UnityWebRequest webRequest)
        {
            return new HttpTransportResponse(
                ToOutcome(webRequest.result),
                webRequest.responseCode,
                webRequest.downloadHandler.data ?? Array.Empty<byte>(),
                webRequest.GetResponseHeaders() ?? NoHeaders,
                webRequest.error ?? string.Empty);
        }

        private static HttpTransportOutcome ToOutcome(UnityWebRequest.Result result)
        {
            return result switch
            {
                UnityWebRequest.Result.Success => HttpTransportOutcome.Success,
                UnityWebRequest.Result.ConnectionError => HttpTransportOutcome.ConnectionError,
                UnityWebRequest.Result.ProtocolError => HttpTransportOutcome.ProtocolError,
                UnityWebRequest.Result.DataProcessingError => HttpTransportOutcome.DataProcessingError,
                _ => throw new InvalidOperationException($"Web request finished with result {result}."),
            };
        }

        private static string ToVerb(HttpMethod method)
        {
            return method switch
            {
                HttpMethod.Get => UnityWebRequest.kHttpVerbGET,
                HttpMethod.Post => UnityWebRequest.kHttpVerbPOST,
                HttpMethod.Put => UnityWebRequest.kHttpVerbPUT,
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method."),
            };
        }
    }
}
