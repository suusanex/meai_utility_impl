namespace MeAiUtility.MultiProvider.CodexAppServer.Options;

public sealed class CodexAppServerProviderOptions
{
    public string CodexCommand { get; set; } = "codex";
    public IReadOnlyList<string> CodexArguments { get; set; } = ["app-server"];
    public string Transport { get; set; } = "stdio";
    public string? ModelId { get; set; }
    public string ReasoningEffort { get; set; } = "medium";
    public string? WorkingDirectory { get; set; }
    public string ApprovalPolicy { get; set; } = "never";
    public string SandboxMode { get; set; } = "workspace-write";
    public bool NetworkAccess { get; set; }
    public int TimeoutSeconds { get; set; } = 1800;
    public bool AutoApprove { get; set; } = true;
    public bool CaptureEventsForDiagnostics { get; set; }
    public string? ServiceName { get; set; }
    public string? Summary { get; set; }
    public string? Personality { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}
