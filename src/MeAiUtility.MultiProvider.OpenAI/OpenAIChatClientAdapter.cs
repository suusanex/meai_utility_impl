extern alias OfficialMeAi;
extern alias OfficialMeAiOpenAI;

using System.Threading.Channels;
using System.ClientModel;
using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.OpenAI.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OfficialChatClient = OfficialMeAi::Microsoft.Extensions.AI.IChatClient;

namespace MeAiUtility.MultiProvider.OpenAI;

public class OpenAIChatClientAdapter : IChatClient, IProviderCapabilities
{
    private readonly ILogger<OpenAIChatClientAdapter> _logger;
    private readonly OpenAIProviderOptions _options;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> _responseInvoker;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> _streamingInvoker;

    public OpenAIChatClientAdapter(ILogger<OpenAIChatClientAdapter> logger, OpenAIProviderOptions options)
    {
        _logger = logger;
        _options = options;

        var innerChatClient = CreateInnerChatClient(options);
        _responseInvoker = CreateResponseInvoker(innerChatClient, options.ModelName);
        _streamingInvoker = CreateStreamingInvoker(innerChatClient, options.ModelName);
    }

    internal OpenAIChatClientAdapter(
        ILogger<OpenAIChatClientAdapter> logger,
        OpenAIProviderOptions options,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> responseInvoker,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> streamingInvoker)
    {
        _logger = logger;
        _options = options;
        _responseInvoker = responseInvoker;
        _streamingInvoker = streamingInvoker;
    }

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

    public virtual async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "OpenAI");
        ValidateExtensions(optionsArg, "openai", "OpenAI", _logger);
        using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

        try
        {
            return await _responseInvoker(messages, optionsArg, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw OpenAIProviderExecution.CreateTimeout(_logger, "OpenAI", _options.TimeoutSeconds, ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            throw OpenAIProviderExecution.MapFailure(_logger, ex, "OpenAI");
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "OpenAI");
        ValidateExtensions(options, "openai", "OpenAI", _logger);

        return StreamUpdatesCore();

        async IAsyncEnumerable<ChatResponseUpdate> StreamUpdatesCore([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
        {
            using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

            IAsyncEnumerable<ChatResponseUpdate> updates;
            try
            {
                updates = _streamingInvoker(messages, options, timeoutCts.Token);
            }
            catch (Exception ex) when (ex is not MultiProviderException)
            {
                throw OpenAIProviderExecution.MapFailure(_logger, ex, "OpenAI");
            }

            await foreach (var update in StreamUpdates(updates, timeoutCts, cancellationToken, _logger, _options.TimeoutSeconds, enumerationCancellationToken))
            {
                yield return update;
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;

    public void Dispose()
    {
    }

    public static void ValidateExtensions(ChatOptions? options, string expectedPrefix, string providerName, ILogger? logger)
    {
        if (options?.AdditionalProperties is null
            || !options.AdditionalProperties.TryGetValue("meai.extensions", out var raw)
            || raw is not ExtensionParameters ext)
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

    private static OfficialChatClient CreateInnerChatClient(OpenAIProviderOptions options)
    {
        options.Validate();

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(options.BaseUrl, UriKind.Absolute);
        }

        if (!string.IsNullOrWhiteSpace(options.OrganizationId))
        {
            clientOptions.OrganizationId = options.OrganizationId;
        }

        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
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
        CancellationTokenSource timeoutCts,
        CancellationToken callerCancellationToken,
        ILogger logger,
        int timeoutSeconds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<ChatResponseUpdate>();

        _ = Task.Run(async () =>
        {
            using (timeoutCts)
            {
                using var linkedEnumerationCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, enumerationCancellationToken);

                try
                {
                    await foreach (var update in updates.WithCancellation(linkedEnumerationCts.Token))
                    {
                        await channel.Writer.WriteAsync(update, linkedEnumerationCts.Token);
                    }

                    channel.Writer.TryComplete();
                }
                catch (OperationCanceledException ex) when (!callerCancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
                {
                    channel.Writer.TryComplete(OpenAIProviderExecution.CreateTimeout(logger, "OpenAI", timeoutSeconds, ex));
                }
                catch (Exception ex) when (ex is not MultiProviderException)
                {
                    channel.Writer.TryComplete(OpenAIProviderExecution.MapFailure(logger, ex, "OpenAI"));
                }
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
