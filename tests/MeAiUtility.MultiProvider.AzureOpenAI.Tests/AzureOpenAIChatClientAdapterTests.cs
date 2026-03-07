using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class AzureOpenAIChatClientAdapterTests
{
    [Test]
    public async Task GetResponseAsync_ReturnsResponse()
    {
        var sut = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());
        Assert.That(response.Message.Text, Does.Contain("AzureOpenAI"));
    }
}
