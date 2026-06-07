namespace MultiCodingAgentFacade.Core.Exceptions;

public sealed class RuntimeAuthenticationException(string message, string runtimeName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : RuntimeFacadeException(message, runtimeName, traceId, statusCode, responseBody, innerException);

public sealed class RuntimeRateLimitException(string message, string runtimeName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : RuntimeFacadeException(message, runtimeName, traceId, statusCode, responseBody, innerException);

public sealed class RuntimeInvalidRequestException(string message, string runtimeName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : RuntimeFacadeException(message, runtimeName, traceId, statusCode, responseBody, innerException);

public sealed class RuntimeOperationException(string message, string runtimeName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : RuntimeFacadeException(message, runtimeName, traceId, statusCode, responseBody, innerException);

public sealed class RuntimeTimeoutException : RuntimeFacadeException
{
    public RuntimeTimeoutException(string message, string runtimeName, int timeoutSeconds, string? traceId = null, Exception? innerException = null)
        : base(message, runtimeName, traceId, null, null, innerException)
    {
        TimeoutSeconds = timeoutSeconds;
    }

    public int TimeoutSeconds { get; }
}

public sealed class RuntimeFeatureNotSupportedException : RuntimeFacadeException
{
    public RuntimeFeatureNotSupportedException(string message, string runtimeName, string featureName, string? traceId = null, Exception? innerException = null)
        : base(message, runtimeName, traceId, null, null, innerException)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }
}
