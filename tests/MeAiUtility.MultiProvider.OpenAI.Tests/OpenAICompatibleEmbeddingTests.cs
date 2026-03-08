using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.Exceptions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAICompatibleEmbeddingTests
{
    [Test]
    public void GenerateEmbeddingAsync_ThrowsNotSupported()
    {
        var sut = new OpenAICompatibleEmbeddingAdapter();
        Assert.That(async () => await sut.GenerateEmbeddingAsync("test"), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>());
    }
}
