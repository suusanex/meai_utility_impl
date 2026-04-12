using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotChatClient(CopilotClientHost host, GitHubCopilotProviderOptions options, ILogger<GitHubCopilotChatClient> logger) : IChatClient, IProviderCapabilities, ICopilotModelCatalog
{
    public bool SupportsReasoningEffort => true;
    public bool SupportsStreaming => true;
    public bool SupportsModelDiscovery => true;
    public bool SupportsEmbeddings => false;
    public bool SupportsProviderOverride => true;
    public bool SupportsExtensionParameters => true;

    public bool IsSupported(FeatureName featureName) => featureName switch
    {
        FeatureName.Streaming => true,
        FeatureName.ReasoningEffort => true,
        FeatureName.ModelDiscovery => true,
        FeatureName.ProviderOverride => true,
        FeatureName.ExtensionParameters => true,
        _ => false,
    };

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg) ?? new ConversationExecutionOptions();
        ValidateExecutionOptions(optionsArg, execution);
        var modelId = execution.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = execution.ReasoningEffort ?? options.ReasoningEffort;

        var models = await ListModelsAsync(cancellationToken);
        var selected = models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            throw new InvalidRequestException($"Unknown GitHub Copilot model id '{modelId}'. Valid CLI model ids: {string.Join(", ", models.Select(static model => model.ModelId))}", "GitHubCopilot");
        }

        if (reasoning is not null && selected is not null && !selected.SupportsReasoningEffort)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Reasoning effort is not supported by selected model.", "GitHubCopilot", "ReasoningEffort");
        }

        var config = new CopilotSessionConfig
        {
            ModelId = modelId,
            ReasoningEffort = reasoning,
            Streaming = execution.Streaming ?? options.Streaming,
            Attachments = execution.Attachments?.Select(static attachment => new FileAttachment
            {
                Path = attachment.Path,
                DisplayName = attachment.DisplayName,
            }).ToArray(),
            SkillDirectories = execution.SkillDirectories?.ToArray(),
            DisabledSkills = execution.DisabledSkills?.ToArray(),
            TimeoutSeconds = execution.TimeoutSeconds,
            ProviderOverride = execution.ProviderOverride ?? options.ProviderOverride,
        };

        ValidateExtensions(optionsArg, config);

        var prompt = string.Join("\n", messages.Select(FormatMessage));
        try
        {
            var text = await host.Wrapper.SendAsync(prompt, config, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new CopilotRuntimeException(
                "Failed to execute Copilot chat request.",
                "GitHubCopilot",
                options.CliPath,
                null,
                traceId,
                ex,
                CopilotOperation.Send);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var chunk in response.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk + " ");
        }
    }

    public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
        => host.ListModelsAsync(cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType switch
    {
        _ when serviceType == typeof(IProviderCapabilities) => this,
        _ when serviceType == typeof(ICopilotModelCatalog) => this,
        _ => null,
    };
    public void Dispose() { }

    private static void ValidateExecutionOptions(ChatOptions? optionsArg, ConversationExecutionOptions execution)
    {
        if (optionsArg?.ResponseFormat is not null)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException(
                "ResponseFormat is not supported by GitHubCopilot provider.",
                "GitHubCopilot",
                "ResponseFormat");
        }

        if (execution.TimeoutSeconds is <= 0)
        {
            throw new InvalidRequestException("TimeoutSeconds must be greater than zero.", "GitHubCopilot");
        }

        if (execution.Attachments is null)
        {
            return;
        }

        foreach (var attachment in execution.Attachments)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Path))
            {
                throw new InvalidRequestException("Attachment path must be specified.", "GitHubCopilot");
            }

            if (!Path.IsPathRooted(attachment.Path))
            {
                throw new InvalidRequestException("Attachment path must be an absolute path.", "GitHubCopilot");
            }
        }
    }

    private static void ValidateExtensions(ChatOptions? optionsArg, CopilotSessionConfig config)
    {
        if (optionsArg?.AdditionalProperties is not null
            && optionsArg.AdditionalProperties.TryGetValue("meai.extensions", out var raw))
        {
            if (raw is not ExtensionParameters ext)
            {
                throw new InvalidRequestException("ChatOptions meai.extensions must be ExtensionParameters.", "GitHubCopilot");
            }

            foreach (var kv in ext.GetAllForProvider("copilot"))
            {
                if (ShouldIgnoreExtensionByTypedOverride(kv.Key, config))
                {
                    continue;
                }

                config.AdvancedOptions[kv.Key] = kv.Value;
            }

            var disallowed = ext.GetAllForProvider("openai").Concat(ext.GetAllForProvider("azure")).ToArray();
            if (disallowed.Length > 0)
            {
                throw new InvalidRequestException("Unsupported extension prefix for provider.", "GitHubCopilot");
            }
        }

        config.SkillDirectories ??= GetOptionalStringList(config.AdvancedOptions, "copilot.skillDirectories", "copilot.skill_directories");
        config.DisabledSkills ??= GetOptionalStringList(config.AdvancedOptions, "copilot.disabledSkills", "copilot.disabled_skills");
    }

    private static bool ShouldIgnoreExtensionByTypedOverride(string key, CopilotSessionConfig config)
    {
        if (config.SkillDirectories is not null &&
            (string.Equals(key, "copilot.skillDirectories", StringComparison.OrdinalIgnoreCase)
             || string.Equals(key, "copilot.skill_directories", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (config.DisabledSkills is not null &&
            (string.Equals(key, "copilot.disabledSkills", StringComparison.OrdinalIgnoreCase)
             || string.Equals(key, "copilot.disabled_skills", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string>? GetOptionalStringList(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is IEnumerable<string> typed)
            {
                return typed.ToArray();
            }

            throw new InvalidRequestException($"Extension '{key}' must be an array of strings.", "GitHubCopilot");
        }

        return null;
    }

    private static string FormatMessage(ChatMessage message) => $"{GetRoleDisplayName(message.Role)}: {message.Text}";

    private static string GetRoleDisplayName(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return "User";
        }

        if (role == ChatRole.System)
        {
            return "System";
        }

        if (role == ChatRole.Assistant)
        {
            return "Assistant";
        }

        if (role == ChatRole.Tool)
        {
            return "Tool";
        }

        return role.ToString();
    }
}
