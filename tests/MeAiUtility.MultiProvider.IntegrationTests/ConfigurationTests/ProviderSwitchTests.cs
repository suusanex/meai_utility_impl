using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using MeAiUtility.MultiProvider.IntegrationTests;

namespace MeAiUtility.MultiProvider.IntegrationTests.ConfigurationTests;

public class ProviderSwitchTests
{
    [TestCase("OpenAI")]
    [TestCase("AzureOpenAI")]
    [TestCase("OpenAICompatible")]
    [TestCase("GitHubCopilot")]
    public void ResolvesProvider(string provider)
    {
        IChatClient client = provider switch
        {
            "OpenAI" => new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>(), ProviderTestFactories.CreateOpenAIOptions()),
            "AzureOpenAI" => new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>(), ProviderTestFactories.CreateAzureOptions()),
            "OpenAICompatible" => new OpenAICompatibleProvider(new NullLogger<OpenAICompatibleProvider>(), new MeAiUtility.MultiProvider.OpenAI.Options.OpenAICompatibleProviderOptions { BaseUrl = "http://localhost", ModelName = "local" }),
            "GitHubCopilot" => new GitHubCopilotChatClient(
                new CopilotClientHost(new StubWrapper(), new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()),
                new GitHubCopilotProviderOptions(),
                new NullLogger<GitHubCopilotChatClient>()),
            _ => throw new InvalidOperationException(),
        };

        Assert.That(client, Is.Not.Null);
    }

    private sealed class StubWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }
}
