namespace MeAiUtility.MultiProvider.Options;

public sealed class MultiProviderOptions
{
    public const string SectionName = "MultiProvider";
    public string Provider { get; set; } = string.Empty;
    public object? OpenAI { get; set; }
    public object? AzureOpenAI { get; set; }
    public object? OpenAICompatible { get; set; }
    public object? GitHubCopilot { get; set; }
    public CommonProviderOptions Common { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new InvalidOperationException("Provider must be configured.");
        }

        var valid = Provider switch
        {
            "OpenAI" => OpenAI is not null,
            "AzureOpenAI" => AzureOpenAI is not null,
            "OpenAICompatible" => OpenAICompatible is not null,
            "GitHubCopilot" => GitHubCopilot is not null,
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidOperationException($"Provider '{Provider}' is invalid or missing provider-specific section.");
        }
    }
}
