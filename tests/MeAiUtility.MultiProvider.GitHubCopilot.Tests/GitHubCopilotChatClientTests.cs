using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotChatClientTests
{
    [Test]
    public async Task GetResponseAsync_ConvertsSessionConfig()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions { ModelId = "gpt-5", ReasoningEffort = ReasoningEffortLevel.High };
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Message.Text, Is.EqualTo("ok"));
    }

    [Test]
    public void GetResponseAsync_DoesNotDoubleWrap_MultiProviderException()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        var originalException = new CopilotRuntimeException("inner", "GitHubCopilot", null, null, "trace123");
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(originalException);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.That(ex, Is.SameAs(originalException), "MultiProviderException は二重ラップされずにそのまま再スローされること");
    }
}
