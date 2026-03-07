using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAIChatClientAdapterTests
{
    [Test]
    public async Task GetResponseAsync_ReturnsResponse()
    {
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var options = new ChatOptions { Temperature = 0.5f, MaxOutputTokens = 100 };
        options.StopSequences = ["stop"];

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Message.Text, Does.Contain("OpenAI response"));
    }
}
