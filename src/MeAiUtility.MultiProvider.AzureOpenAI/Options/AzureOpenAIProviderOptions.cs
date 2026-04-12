namespace MeAiUtility.MultiProvider.AzureOpenAI.Options;

public sealed class AzureOpenAIProviderOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-06-01";
    public AzureAuthenticationOptions Authentication { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 60;
}
