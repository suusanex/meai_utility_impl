using MeAiUtility.MultiProvider.CodexAppServer.Threading;

namespace MeAiUtility.MultiProvider.CodexAppServer.Options;

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
    bool CaptureEventsForDiagnostics);
