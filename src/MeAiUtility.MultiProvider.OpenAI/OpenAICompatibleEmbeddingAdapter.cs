using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAICompatibleEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> inputs, EmbeddingGenerationOptions? options, CancellationToken cancellationToken = default)
        => throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Embeddings are not supported by OpenAICompatible.", "OpenAICompatible", "Embeddings");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
