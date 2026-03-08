using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class ExtensionParametersAzureTests
{
    [Test]
    public void RejectsOtherPrefix()
    {
        var ext = new ExtensionParameters();
        ext.Set("openai.top_logprobs", 5);
        var options = new ChatOptions();
        options.AdditionalProperties["meai.extensions"] = ext;
        var sut = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());

        Assert.That(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>());
    }
}
