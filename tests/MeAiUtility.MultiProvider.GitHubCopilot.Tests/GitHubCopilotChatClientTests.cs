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

    [Test]
    public void GetResponseAsync_ThrowsInvalidRequest_WhenModelIdIsNotInCatalog()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5-mini", false), new CopilotModelInfo("gpt-5", true)]);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions { ModelId = "GPT-5 mini" };

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex!.Message, Does.Contain("Unknown GitHub Copilot model id 'GPT-5 mini'"));
        Assert.That(ex.Message, Does.Contain("gpt-5-mini"));
    }

    [Test]
    public async Task GetService_ReturnsCopilotModelCatalog()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5-mini", false)]);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var catalog = sut.GetService(typeof(ICopilotModelCatalog)) as ICopilotModelCatalog;
        var models = await catalog!.ListModelsAsync();

        Assert.That(catalog, Is.Not.Null);
        Assert.That(models.Select(static x => x.ModelId), Is.EqualTo(new[] { "gpt-5-mini" }));
    }
}
