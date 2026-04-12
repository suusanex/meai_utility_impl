extern alias OfficialMeAi;
extern alias OfficialMeAiOpenAI;

using System.ClientModel;
using System.Threading.Channels;
using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.OpenAI.Options;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OfficialChatClient = OfficialMeAi::Microsoft.Extensions.AI.IChatClient;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAICompatibleProvider : IChatClient, IProviderCapabilities
{
    private readonly ILogger<OpenAICompatibleProvider> logger;
    private readonly OpenAICompatibleProviderOptions options;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> _responseInvoker;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> _streamingInvoker;

    public OpenAICompatibleProvider(ILogger<OpenAICompatibleProvider> logger, OpenAICompatibleProviderOptions options)
        : this(logger, options, CreateInnerChatClient(options))
    {
    }

    internal OpenAICompatibleProvider(
        ILogger<OpenAICompatibleProvider> logger,
        OpenAICompatibleProviderOptions options,
        OfficialChatClient innerChatClient)
        : this(
            logger,
            options,
            CreateResponseInvoker(innerChatClient, options.ModelName),
            CreateStreamingInvoker(innerChatClient, options.ModelName))
    {
    }

    internal OpenAICompatibleProvider(
        ILogger<OpenAICompatibleProvider> logger,
        OpenAICompatibleProviderOptions options,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> responseInvoker,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> streamingInvoker)
    {
        this.logger = logger;
        this.options = options;
        _responseInvoker = responseInvoker;
        _streamingInvoker = streamingInvoker;
    }

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

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveOptions = NormalizeOptions(optionsArg);
        using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, options.TimeoutSeconds);

        try
        {
            return await _responseInvoker(messages, effectiveOptions, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw OpenAIProviderExecution.CreateTimeout(logger, "OpenAICompatible", options.TimeoutSeconds, ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            throw OpenAIProviderExecution.MapFailure(logger, ex, "OpenAICompatible");
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = NormalizeOptions(optionsArg);
        return StreamUpdatesCore();

        async IAsyncEnumerable<ChatResponseUpdate> StreamUpdatesCore([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
        {
            using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, options.TimeoutSeconds);

            IAsyncEnumerable<ChatResponseUpdate> updates;
            try
            {
                updates = _streamingInvoker(messages, effectiveOptions, timeoutCts.Token);
            }
            catch (Exception ex) when (ex is not MultiProviderException)
            {
                throw OpenAIProviderExecution.MapFailure(logger, ex, "OpenAICompatible");
            }

            await foreach (var update in StreamUpdates(updates, timeoutCts.Token, cancellationToken, logger, options.TimeoutSeconds, enumerationCancellationToken))
            {
                yield return update;
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;

    public void Dispose()
    {
    }

    private ChatOptions NormalizeOptions(ChatOptions? optionsArg)
    {
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

        var normalizedExecution = execution is null
            ? new ConversationExecutionOptions()
            : new ConversationExecutionOptions
            {
                ReasoningEffort = execution.ReasoningEffort,
                SystemMessageMode = execution.SystemMessageMode,
                AllowedTools = execution.AllowedTools,
                ExcludedTools = execution.ExcludedTools,
                Attachments = execution.Attachments,
                SkillDirectories = execution.SkillDirectories,
                DisabledSkills = execution.DisabledSkills,
                TimeoutSeconds = execution.TimeoutSeconds,
                ClientName = execution.ClientName,
                WorkingDirectory = execution.WorkingDirectory,
                Streaming = execution.Streaming,
                ProviderOverride = execution.ProviderOverride,
            };

        normalizedExecution.ModelId = model;

        var effectiveOptions = optionsArg ?? new ChatOptions();
        (effectiveOptions.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = normalizedExecution;
        return effectiveOptions;
    }

    private static OfficialChatClient CreateInnerChatClient(OpenAICompatibleProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("OpenAICompatible BaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelName))
        {
            throw new InvalidOperationException("OpenAICompatible ModelName is required.");
        }

        var apiKey = string.IsNullOrWhiteSpace(options.ApiKey) ? "compat-placeholder-key" : options.ApiKey;
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.BaseUrl, UriKind.Absolute),
        };

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        var chatClient = client.GetChatClient(options.ModelName);
        return OfficialMeAiOpenAI::Microsoft.Extensions.AI.OpenAIClientExtensions.AsIChatClient(chatClient);
    }

    private static Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> CreateResponseInvoker(
        OfficialChatClient innerChatClient,
        string defaultModelId)
    {
        return async (messages, options, cancellationToken) =>
        {
            var response = await innerChatClient.GetResponseAsync(
                OpenAIOfficialBridge.ToOfficialMessages(messages),
                OpenAIOfficialBridge.ToOfficialChatOptions(options, defaultModelId),
                cancellationToken);

            return OpenAIOfficialBridge.FromOfficialResponse(response);
        };
    }

    private static Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> CreateStreamingInvoker(
        OfficialChatClient innerChatClient,
        string defaultModelId)
    {
        return (messages, options, cancellationToken) => ConvertStreamingUpdates(
            innerChatClient.GetStreamingResponseAsync(
                OpenAIOfficialBridge.ToOfficialMessages(messages),
                OpenAIOfficialBridge.ToOfficialChatOptions(options, defaultModelId),
                cancellationToken));
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamUpdates(
        IAsyncEnumerable<ChatResponseUpdate> updates,
        CancellationToken timeoutToken,
        CancellationToken callerCancellationToken,
        ILogger logger,
        int timeoutSeconds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<ChatResponseUpdate>();

        _ = Task.Run(async () =>
        {
            using var linkedEnumerationCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, enumerationCancellationToken);

            try
            {
                await foreach (var update in updates.WithCancellation(linkedEnumerationCts.Token))
                {
                    await channel.Writer.WriteAsync(update, linkedEnumerationCts.Token);
                }

                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException ex) when (!callerCancellationToken.IsCancellationRequested && timeoutToken.IsCancellationRequested)
            {
                channel.Writer.TryComplete(OpenAIProviderExecution.CreateTimeout(logger, "OpenAICompatible", timeoutSeconds, ex));
            }
            catch (Exception ex) when (ex is not MultiProviderException)
            {
                channel.Writer.TryComplete(OpenAIProviderExecution.MapFailure(logger, ex, "OpenAICompatible"));
            }
        }, CancellationToken.None);

        await foreach (var update in channel.Reader.ReadAllAsync(enumerationCancellationToken))
        {
            yield return update;
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ConvertStreamingUpdates(
        IAsyncEnumerable<OfficialMeAi::Microsoft.Extensions.AI.ChatResponseUpdate> updates,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, update.Text);
        }
    }
}


