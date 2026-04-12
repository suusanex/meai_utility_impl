using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using MeAiUtility.MultiProvider.Exceptions;
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
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        var sut = new AzureOpenAIChatClientAdapter(
            new NullLogger<AzureOpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unexpected"))),
            static (_, _, _) => EmptyUpdates());

        Assert.That(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options),
            Throws.InstanceOf<InvalidRequestException>());
    }

    private static AzureOpenAIProviderOptions CreateOptions() => new()
    {
        Endpoint = "https://example.openai.azure.com",
        DeploymentName = "gpt-4o-mini",
        ApiVersion = "2024-06-01",
        Authentication = new AzureAuthenticationOptions
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
        },
    };

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }
}


