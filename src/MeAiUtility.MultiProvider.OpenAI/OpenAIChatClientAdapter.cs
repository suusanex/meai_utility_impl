using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.OpenAI;

public class OpenAIChatClientAdapter(ILogger<OpenAIChatClientAdapter> logger) : IChatClient, IProviderCapabilities
{
    private readonly ILogger<OpenAIChatClientAdapter> _logger = logger;
    public bool SupportsReasoningEffort => true;
    public bool SupportsStreaming => true;
    public bool SupportsModelDiscovery => false;
    public bool SupportsEmbeddings => true;
    public bool SupportsProviderOverride => false;
    public bool SupportsExtensionParameters => true;

    public bool IsSupported(FeatureName featureName) => featureName switch
    {
        FeatureName.Streaming => true,
        FeatureName.ReasoningEffort => true,
        FeatureName.Embeddings => true,
        FeatureName.ExtensionParameters => true,
        _ => false,
    };

    public virtual Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "OpenAI");
        ValidateExtensions(options, "openai", "OpenAI", _logger);
        var model = execution?.ModelId ?? "gpt-4";
        var text = $"OpenAI response ({model})";
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate("OpenAI");
        await Task.Yield();
        yield return new ChatResponseUpdate(" stream");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;

    public void Dispose() { }

    public static void ValidateExtensions(ChatOptions? options, string expectedPrefix, string providerName, ILogger? logger)
    {
        if (options is null || !options.AdditionalProperties.TryGetValue("meai.extensions", out var raw) || raw is not ExtensionParameters ext)
        {
            return;
        }

        foreach (var kv in ext.GetAllForProvider(expectedPrefix))
        {
            _ = kv;
        }

        var disallowed = new[] { "openai", "azure", "copilot" }
            .Where(prefix => !string.Equals(prefix, expectedPrefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(prefix => ext.GetAllForProvider(prefix))
            .ToArray();

        if (disallowed.Length > 0)
        {
            var trace = Guid.NewGuid().ToString("N");
            var ex = new InvalidRequestException("Unsupported extension prefix for provider.", providerName, trace);
            logger?.LogExceptionWithTrace(ex, trace);
            throw ex;
        }
    }
}
