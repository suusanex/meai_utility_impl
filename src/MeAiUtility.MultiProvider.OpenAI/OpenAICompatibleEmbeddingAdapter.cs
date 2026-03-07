using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAICompatibleEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
        => throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Embeddings are not supported by OpenAICompatible.", "OpenAICompatible", "Embeddings");
}
