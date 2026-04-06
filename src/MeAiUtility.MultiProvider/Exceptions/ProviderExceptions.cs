namespace MeAiUtility.MultiProvider.Exceptions;

public sealed class AuthenticationException(string message, string providerName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : MultiProviderException(message, providerName, traceId, statusCode, responseBody, innerException);

public sealed class RateLimitException(string message, string providerName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : MultiProviderException(message, providerName, traceId, statusCode, responseBody, innerException);

public sealed class InvalidRequestException(string message, string providerName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : MultiProviderException(message, providerName, traceId, statusCode, responseBody, innerException);

public sealed class ProviderException(string message, string providerName, string? traceId = null, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
    : MultiProviderException(message, providerName, traceId, statusCode, responseBody, innerException);

public sealed class TimeoutException : MultiProviderException
{
    public TimeoutException(string message, string providerName, int timeoutSeconds, string? traceId = null, Exception? innerException = null)
        : base(message, providerName, traceId, null, null, innerException)
    {
        TimeoutSeconds = timeoutSeconds;
    }

    public int TimeoutSeconds { get; }
}

public sealed class NotSupportedException : MultiProviderException
{
    public NotSupportedException(string message, string providerName, string featureName, string? traceId = null, Exception? innerException = null)
        : base(message, providerName, traceId, null, null, innerException)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }
}

public sealed class CopilotRuntimeException : MultiProviderException
{
    public CopilotRuntimeException(
        string message,
        string providerName,
        string? cliPath,
        int? exitCode,
        string? traceId = null,
        Exception? innerException = null,
        Options.CopilotOperation? operation = null)
        : base(message, providerName, traceId, null, null, innerException)
    {
        CliPath = cliPath;
        ExitCode = exitCode;
        Operation = operation;
    }

    public string? CliPath { get; }
    public int? ExitCode { get; }
    public Options.CopilotOperation? Operation { get; }
}
