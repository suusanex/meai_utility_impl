using System.Collections.ObjectModel;

namespace Microsoft.Extensions.AI;

public enum ChatRole { System, User, Assistant, Tool }

public sealed class ChatMessage(ChatRole role, string text)
{
    public ChatRole Role { get; } = role;
    public string Text { get; } = text;
}

public sealed class ChatResponse(ChatMessage message)
{
    public ChatMessage Message { get; } = message;
    public Dictionary<string, object?> Metadata { get; } = new();
}

public sealed class ChatResponseUpdate(string text)
{
    public string Text { get; } = text;
}

public sealed class ChatOptions
{
    public float? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
    public IReadOnlyList<string>? StopSequences { get; set; }
    public Dictionary<string, object?> AdditionalProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public readonly record struct Embedding<T>(ReadOnlyMemory<T> Vector);

public interface IEmbeddingGenerator<TInput, TEmbedding>
{
    Task<TEmbedding> GenerateEmbeddingAsync(TInput input, CancellationToken cancellationToken = default);
}

public interface IChatClient : IDisposable
{
    Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);
    object? GetService(Type serviceType, object? serviceKey = null);
}
