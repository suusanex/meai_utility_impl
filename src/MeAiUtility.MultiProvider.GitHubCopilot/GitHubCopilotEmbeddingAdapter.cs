using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
        => throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Embeddings are not supported by GitHubCopilot.", "GitHubCopilot", "Embeddings");
}
