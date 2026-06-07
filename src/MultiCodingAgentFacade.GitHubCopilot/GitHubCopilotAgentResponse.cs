namespace MultiCodingAgentFacade.GitHubCopilot;

public sealed record GitHubCopilotAgentResponse(
    string Text,
    string? ModelId,
    string TraceId,
    string RequestId);
