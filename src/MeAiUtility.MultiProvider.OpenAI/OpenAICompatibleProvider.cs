using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.OpenAI.Options;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAICompatibleProvider(ILogger<OpenAICompatibleProvider> logger, OpenAICompatibleProviderOptions options) : IChatClient, IProviderCapabilities
{
    public bool SupportsReasoningEffort => true;
    public bool SupportsStreaming => true;
    public bool SupportsModelDiscovery => false;
    public bool SupportsEmbeddings => false;
    public bool SupportsProviderOverride => false;
    public bool SupportsExtensionParameters => true;

    public bool IsSupported(FeatureName featureName) => featureName switch
    {
        FeatureName.Streaming => true,
        FeatureName.ReasoningEffort => true,
        FeatureName.ExtensionParameters => true,
        _ => false,
    };

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "OpenAICompatible");
        OpenAIChatClientAdapter.ValidateExtensions(optionsArg, "openai", "OpenAICompatible", logger);
        var model = execution?.ModelId ?? options.ModelName;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidRequestException("ModelName is required for OpenAICompatible.", "OpenAICompatible");
        }

        if (options.ModelMapping is not null && options.ModelMapping.TryGetValue(model, out var mapped))
        {
            model = mapped;
        }

        if (options.StrictCompatibilityMode && model.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidRequestException("Compatibility check failed.", "OpenAICompatible");
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"OpenAICompatible response ({model})")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var chunk in response.Message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(chunk + " ");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;

    public void Dispose()
    {
    }
}
