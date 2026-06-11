namespace MultiCodingAgentFacade.CodexAppServer;

internal sealed record CodexRpcTurnResult(
    string Text,
    string? ThreadId,
    string? TurnId,
    string Status,
    string? TraceId,
    string? RequestId,
    string? DiagnosticsSummary,
    string? ErrorSummary,
    string? JsonRpcTurnStartRequestId);

internal sealed record CodexRpcStreamingUpdate(
    CodexAppServerStreamingUpdateKind Kind,
    string? TextDelta,
    string? FinalText,
    string? ThreadId,
    string? TurnId,
    string? Status,
    string? TraceId,
    string? RequestId,
    string? DiagnosticsSummary,
    string? ErrorSummary,
    string? JsonRpcTurnStartRequestId);
