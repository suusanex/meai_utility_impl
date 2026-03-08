using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

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
            new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>()),
            new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>()),
            copilot,
        ];

        foreach (var client in clients)
        {
            var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());
            Assert.That(response.Message.Text, Is.Not.Empty);
        }
    }

    private sealed class MockCopilotWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("copilot");
    }
}
