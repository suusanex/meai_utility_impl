namespace MeAiUtility.MultiProvider.Options;

public sealed class ProviderOverrideOptions
{
    public string Type { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }
    public string? AzureApiVersion { get; set; }
}
