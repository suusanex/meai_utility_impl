using System.Diagnostics;
using MultiCodingAgentFacade.Core.Options;

namespace MultiCodingAgentFacade.Core.Diagnostics;

public sealed class AgentTelemetry
{
    public static readonly ActivitySource ActivitySource = new("MultiCodingAgentFacade", "1.0.0");

    public string TraceId { get; init; } = string.Empty;
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string RuntimeName { get; init; } = string.Empty;
    public string? ModelId { get; init; }
    public ReasoningEffortLevel? ReasoningEffort { get; init; }

    public static (Activity? Activity, AgentTelemetry Telemetry) Start(string runtimeName, string? modelId, ReasoningEffortLevel? reasoningEffort)
    {
        var activity = ActivitySource.StartActivity("AgentRequest");
        var traceId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        activity?.SetTag("gen_ai.system", runtimeName);

        if (modelId is not null)
        {
            activity?.SetTag("gen_ai.request.model", modelId);
        }

        if (reasoningEffort is not null)
        {
            activity?.SetTag("agent.reasoning_effort", reasoningEffort.ToString());
        }

        var telemetry = new AgentTelemetry
        {
            TraceId = traceId,
            RuntimeName = runtimeName,
            ModelId = modelId,
            ReasoningEffort = reasoningEffort,
        };

        return (activity, telemetry);
    }
}
