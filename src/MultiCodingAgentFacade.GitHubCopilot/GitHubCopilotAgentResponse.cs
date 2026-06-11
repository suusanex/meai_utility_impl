namespace MultiCodingAgentFacade.GitHubCopilot;

public sealed record GitHubCopilotAgentResponse(
    string Text,
    string? ModelId,
    string TraceId,
    string RequestId,
    string RuntimeName,
    TimeSpan ElapsedTime,
    string? FinishStatus = null,
    string? DiagnosticsSummary = null,
    IReadOnlyDictionary<string, object?>? SdkMetadata = null);
