using MeAiUtility.MultiProvider.OpenAI;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAIEmbeddingAdapterTests
{
    [Test]
    public async Task GenerateEmbeddingAsync_ReturnsVector()
    {
        var sut = new OpenAIEmbeddingAdapter();
        var embedding = await sut.GenerateEmbeddingAsync("test");
        Assert.That(embedding.Vector.Length, Is.GreaterThan(0));
    }
}
