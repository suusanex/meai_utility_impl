using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAICompatibleProvider(ILogger<OpenAICompatibleProvider> logger, OpenAICompatibleProviderOptions options) : OpenAIChatClientAdapter(logger)
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "OpenAICompatible");
        ValidateExtensions(optionsArg, "openai", "OpenAICompatible", logger);
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
}
