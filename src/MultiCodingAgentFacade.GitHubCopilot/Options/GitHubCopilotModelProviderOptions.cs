namespace MultiCodingAgentFacade.GitHubCopilot.Options;

public enum GitHubCopilotPermissionHandlingMode
{
    ApproveAll,
    DenyAll,
    NoResult,
}

public sealed class GitHubCopilotModelProviderOptions
{
    public string? Type { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }
    public string? AzureApiVersion { get; set; }
}
