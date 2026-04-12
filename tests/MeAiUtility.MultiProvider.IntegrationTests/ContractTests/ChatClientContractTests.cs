using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using MeAiUtility.MultiProvider.IntegrationTests;

namespace MeAiUtility.MultiProvider.IntegrationTests.ContractTests;

public class ChatClientContractTests
{
    [Test]
    public async Task AllProvidersImplementContract()
    {
        var wrapper = new MockCopilotWrapper();
        var copilot = new GitHubCopilotChatClient(new CopilotClientHost(wrapper, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()), new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());
        IChatClient[] clients =
        [
            ProviderTestFactories.CreateOpenAIStub(),
            ProviderTestFactories.CreateAzureStub(),
            copilot,
        ];

        foreach (var client in clients)
        {
            var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());
            Assert.That(response.Text, Is.Not.Empty);
        }
    }

    [Test]
    public async Task GitHubCopilot_ExposesModelCatalogThroughGetService()
    {
        var wrapper = new MockCopilotWrapper();
        var copilot = new GitHubCopilotChatClient(new CopilotClientHost(wrapper, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()), new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var catalog = copilot.GetService(typeof(ICopilotModelCatalog)) as ICopilotModelCatalog;
        var models = await catalog!.ListModelsAsync();

        Assert.That(catalog, Is.Not.Null);
        Assert.That(models.Select(static x => x.ModelId), Contains.Item("gpt-5-mini"));
    }

    private sealed class MockCopilotWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5-mini", false), new CopilotModelInfo("gpt-5", true)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("copilot");
    }
}
