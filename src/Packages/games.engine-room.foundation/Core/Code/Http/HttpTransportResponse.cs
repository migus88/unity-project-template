using System.Collections.Generic;

namespace Core.Http
{
    internal readonly record struct HttpTransportResponse(HttpTransportOutcome Outcome, long StatusCode, byte[] Body, IReadOnlyDictionary<string, string> Headers, string Error);
}
