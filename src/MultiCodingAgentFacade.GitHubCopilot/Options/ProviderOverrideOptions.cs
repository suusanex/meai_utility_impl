namespace MultiCodingAgentFacade.GitHubCopilot.Options;

public sealed class ProviderOverrideOptions
{
    public string? Type { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }
    public string? AzureApiVersion { get; set; }
}
