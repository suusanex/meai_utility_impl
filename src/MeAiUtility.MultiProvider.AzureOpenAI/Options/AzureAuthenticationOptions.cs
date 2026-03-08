namespace MeAiUtility.MultiProvider.AzureOpenAI.Options;

public enum AuthenticationType
{
    ApiKey,
    EntraId,
}

public sealed class AzureAuthenticationOptions
{
    public AuthenticationType Type { get; set; }
    public string? ApiKey { get; set; }

    public void Validate()
    {
        if (Type == AuthenticationType.ApiKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required when AuthenticationType.ApiKey is selected.");
        }

        if (Type == AuthenticationType.EntraId && ApiKey is not null)
        {
            throw new InvalidOperationException("ApiKey must be null when AuthenticationType.EntraId is selected.");
        }
    }
}
