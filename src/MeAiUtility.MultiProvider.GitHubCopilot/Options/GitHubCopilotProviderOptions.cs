using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Options;

public sealed class InfiniteSessionOptions
{
    public bool? Enabled { get; set; }
    public double? BackgroundCompactionThreshold { get; set; }
    public double? BufferExhaustionThreshold { get; set; }
}

public sealed class GitHubCopilotProviderOptions
{
    public string? CliPath { get; set; }
    public IReadOnlyList<string>? CliArgs { get; set; }
    public string? CliUrl { get; set; }
    public bool UseStdio { get; set; } = true;
    public string LogLevel { get; set; } = "info";
    public bool AutoStart { get; set; } = true;
    public bool AutoRestart { get; set; } = true;
    public string? GitHubToken { get; set; }
    public bool? UseLoggedInUser { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public string? ModelId { get; set; }
    public ReasoningEffortLevel? ReasoningEffort { get; set; }
    public SystemMessageMode? SystemMessageMode { get; set; }
    public IReadOnlyList<string>? AvailableTools { get; set; }
    public IReadOnlyList<string>? ExcludedTools { get; set; }
    public string? ClientName { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool? Streaming { get; set; }
    public string? ConfigDir { get; set; }
    public InfiniteSessionOptions? InfiniteSessions { get; set; }
    public ProviderOverrideOptions? ProviderOverride { get; set; }
}
