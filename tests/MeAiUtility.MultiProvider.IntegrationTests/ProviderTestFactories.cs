using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.IntegrationTests;

internal static class ProviderTestFactories
{
    public static OpenAIProviderOptions CreateOpenAIOptions() => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        ModelName = "gpt-4o-mini",
    };

    public static AzureOpenAIProviderOptions CreateAzureOptions() => new()
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

    public static OpenAIChatClientAdapter CreateOpenAIStub(string responseText = "openai")
        => new(
            new NullLogger<OpenAIChatClientAdapter>(),
            CreateOpenAIOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))),
            static (_, _, _) => EmptyUpdates());

    public static AzureOpenAIChatClientAdapter CreateAzureStub(string responseText = "azure")
        => new(
            new NullLogger<AzureOpenAIChatClientAdapter>(),
            CreateAzureOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))),
            static (_, _, _) => EmptyUpdates());

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }
}
