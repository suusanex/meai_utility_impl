namespace MultiCodingAgentFacade.Core.Exceptions;

public class RuntimeFacadeException : Exception
{
    public RuntimeFacadeException(
        string message,
        string runtimeName,
        string? traceId = null,
        int? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RuntimeName = runtimeName;
        TraceId = traceId;
        Timestamp = DateTimeOffset.UtcNow;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string RuntimeName { get; }
    public string? TraceId { get; }
    public DateTimeOffset Timestamp { get; }
    public int? StatusCode { get; }
    public string? ResponseBody { get; }
}
