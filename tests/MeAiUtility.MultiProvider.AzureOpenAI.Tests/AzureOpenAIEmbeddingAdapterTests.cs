using MeAiUtility.MultiProvider.AzureOpenAI;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class AzureOpenAIEmbeddingAdapterTests
{
    [Test]
    public async Task GenerateEmbeddingAsync_ReturnsVector()
    {
        var sut = new AzureOpenAIEmbeddingAdapter();
        var result = await sut.GenerateEmbeddingAsync("hello");
        Assert.That(result.Vector.Length, Is.GreaterThan(0));
    }
}
