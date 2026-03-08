namespace MeAiUtility.MultiProvider.OpenAI.Options;

public sealed class OpenAIProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string? BaseUrl { get; set; }
    public string ModelName { get; set; } = "gpt-4";
    public int TimeoutSeconds { get; set; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("OpenAI ApiKey is required.");
        }
    }
}
