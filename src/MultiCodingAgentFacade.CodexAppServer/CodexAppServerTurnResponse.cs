namespace MultiCodingAgentFacade.CodexAppServer;

public sealed record CodexAppServerTurnResponse(
    string Text,
    string? ThreadId = null,
    string? TurnId = null,
    string? Status = null,
    string? TraceId = null,
    string? RequestId = null,
    string? DiagnosticsSummary = null,
    string? ErrorSummary = null,
    string? JsonRpcTurnStartRequestId = null);
