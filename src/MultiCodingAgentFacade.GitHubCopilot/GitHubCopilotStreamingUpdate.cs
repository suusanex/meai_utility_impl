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
    int? AccumulatedLength = null,
    string? TraceId = null,
    string? RequestId = null,
    string? RuntimeName = null,
    TimeSpan? ElapsedTime = null,
    string? FinishStatus = null,
    string? DiagnosticsSummary = null,
    IReadOnlyDictionary<string, object?>? SdkMetadata = null);
