using Microsoft.Extensions.Logging;

namespace MultiCodingAgentFacade.Core.Diagnostics;

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
}
