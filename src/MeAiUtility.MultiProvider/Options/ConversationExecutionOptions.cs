using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Options;

public sealed class ConversationExecutionOptions
{
    public const string PropertyName = "meai.execution";

    public string? ModelId { get; set; }
    public ReasoningEffortLevel? ReasoningEffort { get; set; }
    public SystemMessageMode? SystemMessageMode { get; set; }
    public IReadOnlyList<string>? AllowedTools { get; set; }
    public IReadOnlyList<string>? ExcludedTools { get; set; }
    public string? ClientName { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool? Streaming { get; set; }
    public ProviderOverrideOptions? ProviderOverride { get; set; }

    public static ConversationExecutionOptions? FromChatOptions(ChatOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        if (!options.AdditionalProperties.TryGetValue(PropertyName, out var value) || value is null)
        {
            return null;
        }

        if (value is ConversationExecutionOptions execution)
        {
            return execution;
        }

        throw new InvalidOperationException("ChatOptions meai.execution must be ConversationExecutionOptions.");
    }
}
