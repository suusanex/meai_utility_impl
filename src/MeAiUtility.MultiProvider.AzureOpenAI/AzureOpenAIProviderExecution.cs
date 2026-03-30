using System.ClientModel;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

internal static class AzureOpenAIProviderExecution
{
    public static CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken, int timeoutSeconds)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1)));
        return timeoutCts;
    }

    public static MeAiUtility.MultiProvider.Exceptions.TimeoutException CreateTimeout(ILogger logger, int timeoutSeconds, OperationCanceledException exception)
    {
        var traceId = Guid.NewGuid().ToString("N");
        logger.LogExceptionWithTrace(exception, traceId);
        return new MeAiUtility.MultiProvider.Exceptions.TimeoutException("The provider request timed out.", "AzureOpenAI", timeoutSeconds, traceId, exception);
    }

    public static MultiProviderException MapFailure(ILogger logger, Exception exception)
    {
        if (exception is ClientResultException clientResultException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            var responseBody = clientResultException.Message;
            logger.LogExceptionWithTrace(clientResultException, traceId);
            logger.LogHttpError(clientResultException.Status, responseBody, traceId);
            return clientResultException.Status switch
            {
                400 or 404 or 409 or 422 => new InvalidRequestException("The provider request was rejected.", "AzureOpenAI", traceId, clientResultException.Status, responseBody, clientResultException),
                401 or 403 => new AuthenticationException("Authentication failed for provider request.", "AzureOpenAI", traceId, clientResultException.Status, responseBody, clientResultException),
                429 => new RateLimitException("The provider rate limit was exceeded.", "AzureOpenAI", traceId, clientResultException.Status, responseBody, clientResultException),
                _ => new ProviderException("The provider request failed.", "AzureOpenAI", traceId, clientResultException.Status, responseBody, clientResultException),
            };
        }

        if (exception is ArgumentException argumentException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(argumentException, traceId);
            return new InvalidRequestException(argumentException.Message, "AzureOpenAI", traceId, null, argumentException.Message, argumentException);
        }

        var fallbackTraceId = Guid.NewGuid().ToString("N");
        logger.LogExceptionWithTrace(exception, fallbackTraceId);
        return new ProviderException("The provider request failed.", "AzureOpenAI", fallbackTraceId, null, exception.Message, exception);
    }
}
