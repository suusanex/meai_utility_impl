using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;

public sealed record CopilotModelInfo(string ModelId, bool SupportsReasoningEffort);

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
    int? AccumulatedLength = null);

public sealed class CopilotSessionConfig
{
    public string? ModelId { get; set; }
    public ReasoningEffortLevel? ReasoningEffort { get; set; }
    public bool? Streaming { get; set; }
    public IReadOnlyList<FileAttachment>? Attachments { get; set; }
    public IReadOnlyList<string>? SkillDirectories { get; set; }
    public IReadOnlyList<string>? DisabledSkills { get; set; }
    public int? TimeoutSeconds { get; set; }
    public ProviderOverrideOptions? ProviderOverride { get; set; }
    public string? TraceId { get; set; }
    public string? RequestId { get; set; }
    public Dictionary<string, object?> AdvancedOptions { get; } = new();
}

public interface ICopilotSdkWrapper
{
    bool SupportsStreaming => false;

    Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default);

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
