using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;

public sealed record CopilotModelInfo(string ModelId, bool SupportsReasoningEffort);

public sealed class CopilotSessionConfig
{
    public string? ModelId { get; set; }
    public ReasoningEffortLevel? ReasoningEffort { get; set; }
    public bool? Streaming { get; set; }
    public ProviderOverrideOptions? ProviderOverride { get; set; }
    public Dictionary<string, object?> AdvancedOptions { get; } = new();
}

public interface ICopilotSdkWrapper
{
    Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default);
}
