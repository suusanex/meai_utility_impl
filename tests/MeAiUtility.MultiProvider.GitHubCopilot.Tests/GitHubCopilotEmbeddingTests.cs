using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotEmbeddingTests
{
    [Test]
    public void GenerateEmbeddingAsync_ThrowsNotSupported()
    {
        var sut = new GitHubCopilotEmbeddingAdapter();
        Assert.That(async () => await sut.GenerateAsync(["x"], new EmbeddingGenerationOptions(), CancellationToken.None), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>());
    }
}
