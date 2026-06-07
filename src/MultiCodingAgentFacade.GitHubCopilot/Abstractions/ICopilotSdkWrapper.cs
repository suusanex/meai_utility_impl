using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot.Options;

namespace MultiCodingAgentFacade.GitHubCopilot.Abstractions;

public sealed record CopilotModelInfo(
    string ModelId,
    IReadOnlyList<string> SupportedReasoningEfforts,
    string? DefaultReasoningEffort = null)
{
    public bool SupportsReasoningEffort => SupportedReasoningEfforts.Count > 0;
}

public enum CopilotStreamingUpdateKind
{
    Delta,
    Progress,
    Completed,
}

public sealed record CopilotStreamingUpdate(
    CopilotStreamingUpdateKind Kind,
    string? TextDelta = null,
    string? FinalText = null,
    int? DeltaCount = null,
    int? AccumulatedLength = null,
    string? FinishStatus = null,
    string? DiagnosticsSummary = null,
    IReadOnlyDictionary<string, object?>? SdkMetadata = null);

public sealed record CopilotSdkResponse(
    string Text,
    string? FinishStatus = null,
    string? DiagnosticsSummary = null,
    IReadOnlyDictionary<string, object?>? SdkMetadata = null);

public sealed class CopilotSessionConfig
{
    public string? ModelId { get; set; }
    public ReasoningEffortLevel? ReasoningEffort { get; set; }
    public bool? Streaming { get; set; }
    public IReadOnlyList<FileAttachment>? Attachments { get; set; }
    public IReadOnlyList<string>? SkillDirectories { get; set; }
    public IReadOnlyList<string>? DisabledSkills { get; set; }
    public int? TimeoutSeconds { get; set; }
    public GitHubCopilotModelProviderOptions? ModelProvider { get; set; }
    public InfiniteSessionOptions? InfiniteSessions { get; set; }
    public GitHubCopilotPermissionHandlingMode PermissionHandling { get; set; } = GitHubCopilotPermissionHandlingMode.ApproveAll;
    public string? TraceId { get; set; }
    public string? RequestId { get; set; }
    public Dictionary<string, object?> AdvancedOptions { get; } = new();
}

public interface ICopilotSdkWrapper
{
    bool SupportsStreaming => false;

    Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<CopilotSdkResponse> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default);

    async IAsyncEnumerable<CopilotStreamingUpdate> SendStreamingAsync(
        string prompt,
        CopilotSessionConfig config,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        throw new InvalidOperationException("Streaming is not supported by this Copilot SDK wrapper.");
    }
}
