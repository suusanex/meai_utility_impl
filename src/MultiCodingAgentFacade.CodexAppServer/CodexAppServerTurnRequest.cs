using MultiCodingAgentFacade.CodexAppServer.Threading;

namespace MultiCodingAgentFacade.CodexAppServer;

public sealed class CodexAppServerTurnRequest
{
    public required string Prompt { get; init; }
    public string? ModelId { get; init; }
    public CodexReasoningEffort? ReasoningEffort { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? ApprovalPolicy { get; init; }
    public string? SandboxMode { get; init; }
    public bool? NetworkAccess { get; init; }
    public int? TimeoutSeconds { get; init; }
    public bool? AutoApprove { get; init; }
    public CodexThreadReusePolicy? ThreadReusePolicy { get; init; }
    public string? ThreadId { get; init; }
    public string? ThreadKey { get; init; }
    public string? ThreadName { get; init; }
    public string? ThreadStorePath { get; init; }
    public string? ServiceName { get; init; }
    public string? Summary { get; init; }
    public string? Personality { get; init; }
    public bool? CaptureEventsForDiagnostics { get; init; }
}
