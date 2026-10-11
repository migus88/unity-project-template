namespace Core.Http
{
    internal enum HttpTransportOutcome
    {
        None = 0,
        Success = 1,
        ConnectionError = 2,
        ProtocolError = 3,
        DataProcessingError = 4,
    }
}
