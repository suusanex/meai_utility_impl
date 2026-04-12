using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class AzureOpenAIEmbeddingAdapterTests
{
    [Test]
    public async Task GenerateEmbeddingAsync_ReturnsInjectedVector()
    {
        var sut = new AzureOpenAIEmbeddingAdapter(
            new NullLogger<AzureOpenAIEmbeddingAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f })])));

        var results = await sut.GenerateAsync(["hello"], new EmbeddingGenerationOptions(), CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Vector.ToArray(), Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
    }

    private static AzureOpenAIProviderOptions CreateOptions() => new()
    {
        Endpoint = "https://example.openai.azure.com",
        DeploymentName = "text-embedding-3-small",
        ApiVersion = "2024-06-01",
        Authentication = new AzureAuthenticationOptions
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
        },
    };
}
