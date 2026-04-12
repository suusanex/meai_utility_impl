using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> inputs, EmbeddingGenerationOptions? options, CancellationToken cancellationToken = default)
        => throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Embeddings are not supported by GitHubCopilot.", "GitHubCopilot", "Embeddings");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
