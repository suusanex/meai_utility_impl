using Microsoft.Extensions.AI;
using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.OpenAI.Options;
using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.Samples;

public static class ProviderSwitchSample
{
    public static async Task<string> RunAsync(string provider)
    {
        IChatClient client = provider switch
        {
            "OpenAI" => new OpenAIChatClientAdapter(
                new NullLogger<OpenAIChatClientAdapter>(),
                new OpenAIProviderOptions
                {
                    ApiKey = "sample-key",
                    BaseUrl = "https://api.openai.com/v1",
                    ModelName = "gpt-4o-mini",
                }),
            "AzureOpenAI" => new AzureOpenAIChatClientAdapter(
                new NullLogger<AzureOpenAIChatClientAdapter>(),
                new AzureOpenAIProviderOptions
                {
                    Endpoint = "https://example.openai.azure.com",
                    DeploymentName = "gpt-4o-mini",
                    ApiVersion = "2024-06-01",
                    Authentication = new AzureAuthenticationOptions
                    {
                        Type = AuthenticationType.ApiKey,
                        ApiKey = "sample-key",
                    },
                }),
            "OpenAICompatible" => new OpenAICompatibleProvider(new NullLogger<OpenAICompatibleProvider>(), new OpenAICompatibleProviderOptions { BaseUrl = "http://localhost", ModelName = "local-model" }),
            "GitHubCopilot" => new GitHubCopilotChatClient(
                new CopilotClientHost(new SampleWrapper(), new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()),
                new GitHubCopilotProviderOptions(),
                new NullLogger<GitHubCopilotChatClient>()),
            _ => throw new InvalidOperationException("Unsupported provider"),
        };

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "switch")]);
        return response.Message.Text;
    }

    private sealed class SampleWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5-mini", false), new CopilotModelInfo("gpt-5", true)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("Copilot response");
    }
}
