extern alias OfficialMeAi;
extern alias OfficialMeAiOpenAI;

using System.Threading.Channels;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OfficialChatClient = OfficialMeAi::Microsoft.Extensions.AI.IChatClient;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

public sealed class AzureOpenAIChatClientAdapter : IChatClient, IProviderCapabilities
{
    private readonly ILogger<AzureOpenAIChatClientAdapter> _logger;
    private readonly AzureOpenAIProviderOptions _options;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> _responseInvoker;
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> _streamingInvoker;

    public AzureOpenAIChatClientAdapter(ILogger<AzureOpenAIChatClientAdapter> logger, AzureOpenAIProviderOptions options)
    {
        _logger = logger;
        _options = options;

        var innerChatClient = CreateInnerChatClient(options);
        _responseInvoker = CreateResponseInvoker(innerChatClient, options.DeploymentName);
        _streamingInvoker = CreateStreamingInvoker(innerChatClient, options.DeploymentName);
    }

    internal AzureOpenAIChatClientAdapter(
        ILogger<AzureOpenAIChatClientAdapter> logger,
        AzureOpenAIProviderOptions options,
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

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "AzureOpenAI");
        ValidateExtensions(optionsArg);
        using var timeoutCts = AzureOpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

        try
        {
            return await _responseInvoker(messages, optionsArg, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw AzureOpenAIProviderExecution.CreateTimeout(_logger, _options.TimeoutSeconds, ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            throw AzureOpenAIProviderExecution.MapFailure(_logger, ex);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(options);
        CopilotOptionGuards.ThrowIfCopilotOnlyOptionsSpecified(execution, "AzureOpenAI");
        ValidateExtensions(options);

        return StreamUpdatesCore();

        async IAsyncEnumerable<ChatResponseUpdate> StreamUpdatesCore([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
        {
            using var timeoutCts = AzureOpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

            IAsyncEnumerable<ChatResponseUpdate> updates;
            try
            {
                updates = _streamingInvoker(messages, options, timeoutCts.Token);
            }
            catch (Exception ex) when (ex is not MultiProviderException)
            {
                throw AzureOpenAIProviderExecution.MapFailure(_logger, ex);
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

    private void ValidateExtensions(ChatOptions? optionsArg)
    {
        if (optionsArg?.AdditionalProperties is null
            || !optionsArg.AdditionalProperties.TryGetValue("meai.extensions", out var raw)
            || raw is not ExtensionParameters ext)
        {
            return;
        }

        var disallowed = ext.GetAllForProvider("openai").Concat(ext.GetAllForProvider("copilot")).ToArray();
        if (disallowed.Length > 0)
        {
            var traceId = Guid.NewGuid().ToString("N");
            var ex = new InvalidRequestException("Unsupported extension prefix for provider.", "AzureOpenAI", traceId);
            _logger.LogExceptionWithTrace(ex, traceId);
            throw ex;
        }
    }

    private static OfficialChatClient CreateInnerChatClient(AzureOpenAIProviderOptions options)
    {
        var client = CreateClient(options);
        var chatClient = client.GetChatClient(options.DeploymentName);
        return OfficialMeAiOpenAI::Microsoft.Extensions.AI.OpenAIClientExtensions.AsIChatClient(chatClient);
    }

    private static AzureOpenAIClient CreateClient(AzureOpenAIProviderOptions options)
    {
        options.Authentication.Validate();

        var endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        var clientOptions = AzureOpenAIOfficialBridge.CreateClientOptions(options.ApiVersion);

        return options.Authentication.Type switch
        {
            AuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(options.Authentication.ApiKey!), clientOptions),
            AuthenticationType.EntraId => new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions),
            _ => throw new InvalidOperationException("Unsupported Azure OpenAI authentication type."),
        };
    }

    private static Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> CreateResponseInvoker(
        OfficialChatClient innerChatClient,
        string defaultDeploymentName)
    {
        return async (messages, options, cancellationToken) =>
        {
            var response = await innerChatClient.GetResponseAsync(
                AzureOpenAIOfficialBridge.ToOfficialMessages(messages),
                AzureOpenAIOfficialBridge.ToOfficialChatOptions(options, defaultDeploymentName),
                cancellationToken);

            return AzureOpenAIOfficialBridge.FromOfficialResponse(response);
        };
    }

    private static Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> CreateStreamingInvoker(
        OfficialChatClient innerChatClient,
        string defaultDeploymentName)
    {
        return (messages, options, cancellationToken) => ConvertStreamingUpdates(
            innerChatClient.GetStreamingResponseAsync(
                AzureOpenAIOfficialBridge.ToOfficialMessages(messages),
                AzureOpenAIOfficialBridge.ToOfficialChatOptions(options, defaultDeploymentName),
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
                    channel.Writer.TryComplete(AzureOpenAIProviderExecution.CreateTimeout(logger, timeoutSeconds, ex));
                }
                catch (Exception ex) when (ex is not MultiProviderException)
                {
                    channel.Writer.TryComplete(AzureOpenAIProviderExecution.MapFailure(logger, ex));
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
