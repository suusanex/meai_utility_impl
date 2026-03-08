using System.Diagnostics;
using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.Telemetry;

public sealed class ChatTelemetry
{
    public static readonly ActivitySource ActivitySource = new("MeAiUtility.MultiProvider", "1.0.0");

    public string TraceId { get; init; } = string.Empty;
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string ProviderId { get; init; } = string.Empty;
    public string? ModelId { get; init; }
    public ReasoningEffortLevel? ReasoningEffort { get; init; }

    public static (Activity? Activity, ChatTelemetry Telemetry) Start(string providerId, string? modelId, ReasoningEffortLevel? reasoningEffort)
    {
        var activity = ActivitySource.StartActivity("ChatRequest");
        var traceId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        activity?.SetTag("gen_ai.provider.name", providerId);
        if (modelId is not null)
        {
            activity?.SetTag("gen_ai.request.model", modelId);
        }

        if (reasoningEffort is not null)
        {
            activity?.SetTag("meai.reasoning_effort", reasoningEffort.ToString());
        }

        var telemetry = new ChatTelemetry
        {
            TraceId = traceId,
            ProviderId = providerId,
            ModelId = modelId,
            ReasoningEffort = reasoningEffort,
        };

        return (activity, telemetry);
    }
}
