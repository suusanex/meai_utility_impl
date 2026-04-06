using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

public sealed class AzureOpenAIChatClientAdapter(ILogger<AzureOpenAIChatClientAdapter> logger) : IChatClient, IProviderCapabilities
{
    private readonly ILogger<AzureOpenAIChatClientAdapter> _logger = logger;
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

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "AzureOpenAI");
        ValidateExtensions(options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "AzureOpenAI response")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate("Azure");
        await Task.Yield();
        yield return new ChatResponseUpdate(" stream");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;
    public void Dispose() { }

    private void ValidateExtensions(ChatOptions? options)
    {
        if (options is null || !options.AdditionalProperties.TryGetValue("meai.extensions", out var raw) || raw is not ExtensionParameters ext)
        {
            return;
        }

        var disallowed = ext.GetAllForProvider("openai").Concat(ext.GetAllForProvider("copilot")).ToArray();
        if (disallowed.Length > 0)
        {
            _logger.LogError("Unsupported extension prefix for AzureOpenAI.");
            throw new InvalidRequestException("Unsupported extension prefix for provider.", "AzureOpenAI");
        }
    }
}
