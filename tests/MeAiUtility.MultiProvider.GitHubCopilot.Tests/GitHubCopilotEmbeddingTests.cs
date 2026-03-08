using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.Exceptions;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotEmbeddingTests
{
    [Test]
    public void GenerateEmbeddingAsync_ThrowsNotSupported()
    {
        var sut = new GitHubCopilotEmbeddingAdapter();
        Assert.That(async () => await sut.GenerateEmbeddingAsync("x"), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>());
    }
}
