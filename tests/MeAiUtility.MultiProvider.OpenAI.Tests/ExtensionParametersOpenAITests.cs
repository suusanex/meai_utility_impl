using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class ExtensionParametersOpenAITests
{
    [Test]
    public void RejectsOtherPrefix()
    {
        var ext = new ExtensionParameters();
        ext.Set("azure.data_sources", new[] { 1 });
        var options = new ChatOptions();
        options.AdditionalProperties["meai.extensions"] = ext;
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());

        Assert.That(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>());
    }
}
