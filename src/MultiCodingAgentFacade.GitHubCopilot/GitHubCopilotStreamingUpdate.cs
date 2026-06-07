namespace MultiCodingAgentFacade.GitHubCopilot;

public enum GitHubCopilotStreamingUpdateKind
{
    Delta,
    Progress,
    Completed,
}

public sealed record GitHubCopilotStreamingUpdate(
    GitHubCopilotStreamingUpdateKind Kind,
    string? TextDelta = null,
    string? FinalText = null,
    int? DeltaCount = null,
    int? AccumulatedLength = null);
