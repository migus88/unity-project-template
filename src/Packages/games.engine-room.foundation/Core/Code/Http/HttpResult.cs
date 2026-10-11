using System;
using OneOf;

namespace Core.Http
{
    [GenerateOneOf]
    public sealed partial class HttpResult : OneOfBase<HttpResult.Completed, HttpResult.HttpError, HttpResult.NetworkError, HttpResult.Timeout>
    {
        public sealed record Completed(HttpResponse Response);

        public sealed record HttpError(HttpResponse Response);

        public readonly record struct NetworkError(string Message);

        public readonly record struct Timeout(TimeSpan After);
    }
}
