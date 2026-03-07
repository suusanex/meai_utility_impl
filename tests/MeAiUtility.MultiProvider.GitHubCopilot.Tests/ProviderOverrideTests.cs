using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class ProviderOverrideTests
{
    [Test]
    public async Task ProviderOverride_IsPassedToSessionConfig()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, c, _) => captured = c)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            ProviderOverride = new ProviderOverrideOptions { Type = "openai", BaseUrl = "https://api.openai.com/v1" },
        };

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.ProviderOverride!.Type, Is.EqualTo("openai"));
    }
}
