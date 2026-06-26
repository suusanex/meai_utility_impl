using System.Text.Json;
using MultiCodingAgentFacade.CodexAppServer.Threading;

namespace MultiCodingAgentFacade.CodexAppServer.Options;

internal sealed record CodexRuntimeOptions(
    string? ModelId,
    string? ReasoningEffort,
    string? WorkingDirectory,
    CodexThreadReusePolicy ThreadReusePolicy,
    string? ThreadId,
    string? ThreadKey,
    string? ThreadName,
    string? ThreadStorePath,
    string ApprovalPolicy,
    string SandboxMode,
    bool NetworkAccess,
    string? ServiceName,
    string? Summary,
    string? Personality,
    bool AutoApprove,
    int TimeoutSeconds,
    string ClientName,
    string ClientVersion,
    bool CaptureEventsForDiagnostics,
    JsonElement? OutputSchema);
