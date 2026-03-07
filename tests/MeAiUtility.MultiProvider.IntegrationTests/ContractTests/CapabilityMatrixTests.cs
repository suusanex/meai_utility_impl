using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.IntegrationTests.ContractTests;

public class CapabilityMatrixTests
{
    [Test]
    public void CapabilityMatrix_MatchesExpectations()
    {
        IProviderCapabilities openAi = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        IProviderCapabilities azure = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());
        IProviderCapabilities copilot = new GitHubCopilotChatClient(
            new CopilotClientHost(new Wrapper(), new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()),
            new GitHubCopilotProviderOptions(),
            new NullLogger<GitHubCopilotChatClient>());

        Assert.That(openAi.SupportsEmbeddings, Is.True);
        Assert.That(azure.SupportsEmbeddings, Is.True);
        Assert.That(copilot.SupportsEmbeddings, Is.False);
    }

    private sealed class Wrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]);
        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }
}
