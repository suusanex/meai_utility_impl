namespace MultiCodingAgentFacade.CodexAppServer;

public enum CodexAppServerStreamingUpdateKind
{
    Delta,
    StatusChanged,
    Completed,
    Error,
}

public sealed record CodexAppServerStreamingUpdate(
    CodexAppServerStreamingUpdateKind Kind,
    string? TextDelta = null,
    string? FinalText = null,
    string? ThreadId = null,
    string? TurnId = null,
    string? Status = null,
    string? TraceId = null,
    string? RequestId = null,
    string? DiagnosticsSummary = null,
    string? ErrorSummary = null,
    string? JsonRpcTurnStartRequestId = null);
