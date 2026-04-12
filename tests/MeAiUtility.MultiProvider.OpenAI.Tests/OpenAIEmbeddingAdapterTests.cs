using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAIEmbeddingAdapterTests
{
    [Test]
    public async Task GenerateEmbeddingAsync_ReturnsInjectedVector()
    {
        var sut = new OpenAIEmbeddingAdapter(
            new NullLogger<OpenAIEmbeddingAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f })])));

        var embeddings = await sut.GenerateAsync(["test"], new EmbeddingGenerationOptions(), CancellationToken.None);

        Assert.That(embeddings, Has.Count.EqualTo(1));
        Assert.That(embeddings[0].Vector.ToArray(), Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
    }

    private static OpenAIProviderOptions CreateOptions() => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        ModelName = "text-embedding-3-small",
    };
}
