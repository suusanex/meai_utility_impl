extern alias OfficialMeAi;

using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using OpenAI;
using OfficialChatMessage = OfficialMeAi::Microsoft.Extensions.AI.ChatMessage;
using OfficialChatOptions = OfficialMeAi::Microsoft.Extensions.AI.ChatOptions;
using OfficialChatResponse = OfficialMeAi::Microsoft.Extensions.AI.ChatResponse;
using OfficialChatRole = OfficialMeAi::Microsoft.Extensions.AI.ChatRole;
using OfficialReasoningEffort = OfficialMeAi::Microsoft.Extensions.AI.ReasoningEffort;
using OfficialReasoningOptions = OfficialMeAi::Microsoft.Extensions.AI.ReasoningOptions;

namespace MeAiUtility.MultiProvider.OpenAI;

internal static class OpenAIOfficialBridge
{
    public static TimeSpan CreateNetworkTimeout(int timeoutSeconds)
        => TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1));

    public static OpenAIClientOptions CreateClientOptions(string? baseUrl, string? organizationId, int timeoutSeconds)
    {
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = CreateNetworkTimeout(timeoutSeconds),
        };

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            clientOptions.Endpoint = new Uri(baseUrl, UriKind.Absolute);
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            clientOptions.OrganizationId = organizationId;
        }

        return clientOptions;
    }

    public static IReadOnlyList<OfficialChatMessage> ToOfficialMessages(IEnumerable<ChatMessage> messages)
        => messages.Select(message => new OfficialChatMessage(ToOfficialRole(message.Role), message.Text)).ToArray();

    public static OfficialChatOptions ToOfficialChatOptions(ChatOptions? options, string defaultModelId)
    {
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        var official = new OfficialChatOptions
        {
            Temperature = options?.Temperature,
            MaxOutputTokens = options?.MaxOutputTokens,
            ModelId = execution?.ModelId ?? defaultModelId,
            ResponseFormat = options?.ResponseFormat,
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

    private static OfficialChatRole ToOfficialRole(ChatRole role)
    {
        if (role == ChatRole.System)
        {
            return OfficialChatRole.System;
        }

        if (role == ChatRole.User)
        {
            return OfficialChatRole.User;
        }

        if (role == ChatRole.Assistant)
        {
            return OfficialChatRole.Assistant;
        }

        if (role == ChatRole.Tool)
        {
            return OfficialChatRole.Tool;
        }

        return new OfficialChatRole(role.ToString());
    }
}
