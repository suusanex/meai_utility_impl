using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.OpenAI.Options;
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
        var sut = new OpenAIChatClientAdapter(
            new NullLogger<OpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unexpected"))),
            static (_, _, _) => EmptyUpdates());

        Assert.That(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options),
            Throws.InstanceOf<InvalidRequestException>());
    }

    private static OpenAIProviderOptions CreateOptions() => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        ModelName = "gpt-4o-mini",
    };

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }
}
