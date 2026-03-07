using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

public sealed class AzureOpenAIEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = input.Reverse().Select(ch => (float)ch / 255f).Take(8).ToArray();
        return Task.FromResult(new Embedding<float>(vector));
    }
}
