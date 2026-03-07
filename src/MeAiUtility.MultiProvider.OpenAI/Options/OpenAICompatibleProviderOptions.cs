using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.OpenAI.Options;

public sealed class OpenAICompatibleProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public Dictionary<string,string>? ModelMapping { get; set; }
    public bool StrictCompatibilityMode { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
}
