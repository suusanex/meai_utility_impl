namespace MeAiUtility.MultiProvider.Exceptions;

public class MultiProviderException : Exception
{
    public MultiProviderException(
        string message,
        string providerName,
        string? traceId = null,
        int? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        TraceId = traceId;
        Timestamp = DateTimeOffset.UtcNow;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string ProviderName { get; }
    public string? TraceId { get; }
    public DateTimeOffset Timestamp { get; }
    public int? StatusCode { get; }
    public string? ResponseBody { get; }
}
