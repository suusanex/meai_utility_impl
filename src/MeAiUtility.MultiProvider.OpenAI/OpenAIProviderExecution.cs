using System.ClientModel;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.OpenAI;

internal static class OpenAIProviderExecution
{
    public static CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken, int timeoutSeconds)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1)));
        return timeoutCts;
    }

    public static MeAiUtility.MultiProvider.Exceptions.TimeoutException CreateTimeout(ILogger logger, string providerName, int timeoutSeconds, OperationCanceledException exception)
    {
        var traceId = Guid.NewGuid().ToString("N");
        logger.LogExceptionWithTrace(exception, traceId);
        return new MeAiUtility.MultiProvider.Exceptions.TimeoutException("The provider request timed out.", providerName, timeoutSeconds, traceId, exception);
    }

    public static MultiProviderException MapFailure(ILogger logger, Exception exception, string providerName)
    {
        if (exception is ClientResultException clientResultException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            var responseBody = clientResultException.Message;
            logger.LogExceptionWithTrace(clientResultException, traceId);
            logger.LogHttpError(clientResultException.Status, responseBody, traceId);
            return clientResultException.Status switch
            {
                400 or 404 or 409 or 422 => new InvalidRequestException("The provider request was rejected.", providerName, traceId, clientResultException.Status, responseBody, clientResultException),
                401 or 403 => new AuthenticationException("Authentication failed for provider request.", providerName, traceId, clientResultException.Status, responseBody, clientResultException),
                429 => new RateLimitException("The provider rate limit was exceeded.", providerName, traceId, clientResultException.Status, responseBody, clientResultException),
                _ => new ProviderException("The provider request failed.", providerName, traceId, clientResultException.Status, responseBody, clientResultException),
            };
        }

        if (exception is ArgumentException argumentException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(argumentException, traceId);
            return new InvalidRequestException(argumentException.Message, providerName, traceId, null, argumentException.Message, argumentException);
        }

        var fallbackTraceId = Guid.NewGuid().ToString("N");
        logger.LogExceptionWithTrace(exception, fallbackTraceId);
        return new ProviderException("The provider request failed.", providerName, fallbackTraceId, null, exception.Message, exception);
    }
}
