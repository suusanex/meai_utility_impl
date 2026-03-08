using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.Telemetry;

public static class LoggingExtensions
{
    public static string MaskSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "***MASKED***";
    }

    public static void LogExceptionWithTrace(this ILogger logger, Exception ex, string traceId)
    {
        logger.LogError("Unhandled exception. TraceId={TraceId} Exception={Exception}", traceId, ex.ToString());
    }

    public static void LogHttpError(this ILogger logger, int statusCode, string responseBody, string traceId)
    {
        logger.LogError("HTTP request failed. TraceId={TraceId} StatusCode={StatusCode} ResponseBody={ResponseBody}", traceId, statusCode, responseBody);
    }
}
