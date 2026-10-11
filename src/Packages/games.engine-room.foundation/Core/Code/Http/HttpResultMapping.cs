using System;

namespace Core.Http
{
    internal static class HttpResultMapping
    {
        private const long FirstErrorStatus = 400;

        public static HttpResult ToResult(HttpTransportResponse response)
        {
            if (response.Outcome == HttpTransportOutcome.ProtocolError || response.StatusCode >= FirstErrorStatus)
            {
                return new HttpResult.HttpError(ToResponse(response));
            }

            return response.Outcome switch
            {
                HttpTransportOutcome.Success => new HttpResult.Completed(ToResponse(response)),
                HttpTransportOutcome.ConnectionError => new HttpResult.NetworkError(response.Error),
                HttpTransportOutcome.DataProcessingError => new HttpResult.NetworkError(response.Error),
                _ => throw new ArgumentOutOfRangeException(nameof(response), response.Outcome, "Unknown transport outcome."),
            };
        }

        private static HttpResponse ToResponse(HttpTransportResponse response)
        {
            return new HttpResponse(response.StatusCode, response.Body, response.Headers);
        }
    }
}
