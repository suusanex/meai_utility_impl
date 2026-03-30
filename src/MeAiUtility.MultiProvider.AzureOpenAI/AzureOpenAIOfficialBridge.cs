extern alias OfficialMeAi;

using Azure.AI.OpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using OfficialChatMessage = OfficialMeAi::Microsoft.Extensions.AI.ChatMessage;
using OfficialChatOptions = OfficialMeAi::Microsoft.Extensions.AI.ChatOptions;
using OfficialChatResponse = OfficialMeAi::Microsoft.Extensions.AI.ChatResponse;
using OfficialChatRole = OfficialMeAi::Microsoft.Extensions.AI.ChatRole;
using OfficialReasoningEffort = OfficialMeAi::Microsoft.Extensions.AI.ReasoningEffort;
using OfficialReasoningOptions = OfficialMeAi::Microsoft.Extensions.AI.ReasoningOptions;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

internal static class AzureOpenAIOfficialBridge
{
    public static IReadOnlyList<OfficialChatMessage> ToOfficialMessages(IEnumerable<ChatMessage> messages)
        => messages.Select(message => new OfficialChatMessage(ToOfficialRole(message.Role), message.Text)).ToArray();

    public static OfficialChatOptions ToOfficialChatOptions(ChatOptions? options, string defaultDeploymentName)
    {
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        var official = new OfficialChatOptions
        {
            Temperature = options?.Temperature,
            MaxOutputTokens = options?.MaxOutputTokens,
            ModelId = execution?.ModelId ?? defaultDeploymentName,
        };

        if (options?.StopSequences is not null)
        {
            official.StopSequences = [.. options.StopSequences];
        }

        if (execution?.ReasoningEffort is not null)
        {
            official.Reasoning = new OfficialReasoningOptions
            {
                Effort = execution.ReasoningEffort.Value switch
                {
                    ReasoningEffortLevel.Low => OfficialReasoningEffort.Low,
                    ReasoningEffortLevel.Medium => OfficialReasoningEffort.Medium,
                    ReasoningEffortLevel.High => OfficialReasoningEffort.High,
                    ReasoningEffortLevel.XHigh => OfficialReasoningEffort.ExtraHigh,
                    _ => OfficialReasoningEffort.None,
                },
            };
        }

        return official;
    }

    public static ChatResponse FromOfficialResponse(OfficialChatResponse response)
    {
        var text = response.Text;
        if (string.IsNullOrEmpty(text))
        {
            text = response.Messages.LastOrDefault(static message => message.Role == OfficialChatRole.Assistant)?.Text
                ?? response.Messages.LastOrDefault()?.Text
                ?? string.Empty;
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public static AzureOpenAIClientOptions CreateClientOptions(string apiVersion)
    {
        return apiVersion switch
        {
            "2024-10-21" => new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21),
            "2024-06-01" => new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01),
            "2024-02-15-preview" => new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01),
            _ => throw new InvalidOperationException($"Unsupported Azure OpenAI ApiVersion '{apiVersion}'."),
        };
    }

    private static OfficialChatRole ToOfficialRole(ChatRole role) => role switch
    {
        ChatRole.System => OfficialChatRole.System,
        ChatRole.User => OfficialChatRole.User,
        ChatRole.Assistant => OfficialChatRole.Assistant,
        ChatRole.Tool => OfficialChatRole.Tool,
        _ => new OfficialChatRole(role.ToString()),
    };
}
