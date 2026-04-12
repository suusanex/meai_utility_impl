using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAICompatibleEmbeddingTests
{
    [Test]
    public void GenerateEmbeddingAsync_ThrowsNotSupported()
    {
        var sut = new OpenAICompatibleEmbeddingAdapter();
        Assert.That(async () => await sut.GenerateAsync(["test"], new EmbeddingGenerationOptions(), CancellationToken.None), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>());
    }
}
