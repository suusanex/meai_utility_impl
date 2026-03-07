using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotChatClient(CopilotClientHost host, GitHubCopilotProviderOptions options, ILogger<GitHubCopilotChatClient> logger) : IChatClient, IProviderCapabilities
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
        var modelId = execution.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = execution.ReasoningEffort ?? options.ReasoningEffort;

        var models = await host.ListModelsAsync(cancellationToken);
        var selected = models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
        if (reasoning is not null && selected is not null && !selected.SupportsReasoningEffort)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Reasoning effort is not supported by selected model.", "GitHubCopilot", "ReasoningEffort");
        }

        var config = new CopilotSessionConfig
        {
            ModelId = modelId,
            ReasoningEffort = reasoning,
            Streaming = execution.Streaming ?? options.Streaming,
            ProviderOverride = execution.ProviderOverride ?? options.ProviderOverride,
        };

        ValidateExtensions(optionsArg, config);

        var prompt = string.Join("\n", messages.Select(m => m.Text));
        try
        {
            var text = await host.Wrapper.SendAsync(prompt, config, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }
        catch (Exception ex)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new CopilotRuntimeException("Failed to execute Copilot chat request.", "GitHubCopilot", options.CliPath, null, traceId, ex);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var chunk in response.Message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(chunk + " ");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;
    public void Dispose() { }

    private static void ValidateExtensions(ChatOptions? optionsArg, CopilotSessionConfig config)
    {
        if (optionsArg is null || !optionsArg.AdditionalProperties.TryGetValue("meai.extensions", out var raw) || raw is not ExtensionParameters ext)
        {
            return;
        }

        foreach (var kv in ext.GetAllForProvider("copilot"))
        {
            config.AdvancedOptions[kv.Key] = kv.Value;
        }

        var disallowed = ext.GetAllForProvider("openai").Concat(ext.GetAllForProvider("azure")).ToArray();
        if (disallowed.Length > 0)
        {
            throw new InvalidRequestException("Unsupported extension prefix for provider.", "GitHubCopilot");
        }
    }
}
