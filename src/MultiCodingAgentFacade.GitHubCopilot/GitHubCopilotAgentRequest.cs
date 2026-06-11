using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot.Options;

namespace MultiCodingAgentFacade.GitHubCopilot;

public sealed class GitHubCopilotAgentRequest
{
    public required string Prompt { get; init; }
    public string? ModelId { get; init; }
    public ReasoningEffortLevel? ReasoningEffort { get; init; }
    public bool? Streaming { get; init; }
    public IReadOnlyList<FileAttachment>? Attachments { get; init; }
    public IReadOnlyList<string>? SkillDirectories { get; init; }
    public IReadOnlyList<string>? DisabledSkills { get; init; }
    public int? TimeoutSeconds { get; init; }
    public GitHubCopilotModelProviderOptions? ModelProvider { get; init; }
    public InfiniteSessionOptions? InfiniteSessions { get; init; }
    public string? ConfigDir { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<string>? AvailableTools { get; init; }
    public IReadOnlyList<string>? ExcludedTools { get; init; }
    public IReadOnlyDictionary<string, object>? McpServers { get; init; }
    public string? Agent { get; init; }
    public string? Mode { get; init; }
    public GitHubCopilotPermissionHandlingMode? PermissionHandling { get; init; }
    public IReadOnlyDictionary<string, object?>? AdvancedOptions { get; init; }
}
