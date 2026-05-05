using System.Threading.Channels;
using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer;

public sealed class CodexAppServerChatClient(
    CodexAppServerProviderOptions options,
    ICodexTransportFactory transportFactory,
    ILogger<CodexAppServerChatClient> logger,
    ILoggerFactory loggerFactory) : IChatClient, IProviderCapabilities
{
    private const string ProviderName = "CodexAppServer";

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

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? optionsArg = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureTransportIsSupported();

        var runtime = BuildRuntimeOptions(optionsArg);
        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken, runtime.TimeoutSeconds);
        var prompt = string.Join("\n", messages.Select(FormatMessage));

        try
        {
            var transport = transportFactory.Create(runtime.WorkingDirectory);
            var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
            var session = new CodexRpcSession(transport, sessionLogger);
            var text = await session.ExecuteTurnAsync(prompt, runtime, onDelta: null, timeoutCts.Token);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new MeAiUtility.MultiProvider.Exceptions.TimeoutException(
                "Codex App Server request timed out.",
                ProviderName,
                runtime.TimeoutSeconds,
                traceId,
                ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException and not OperationCanceledException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new ProviderException("Failed to execute Codex App Server request.", ProviderName, traceId, null, null, ex);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? optionsArg = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureTransportIsSupported();

        var runtime = BuildRuntimeOptions(optionsArg);
        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken, runtime.TimeoutSeconds);
        var channel = Channel.CreateUnbounded<string>();
        var prompt = string.Join("\n", messages.Select(FormatMessage));
        var emittedDelta = 0;

        var sessionTask = Task.Run(async () =>
        {
            try
            {
                var transport = transportFactory.Create(runtime.WorkingDirectory);
                var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
                var session = new CodexRpcSession(transport, sessionLogger);
                var finalText = await session.ExecuteTurnAsync(
                    prompt,
                    runtime,
                    async delta =>
                    {
                        Interlocked.Exchange(ref emittedDelta, 1);
                        await channel.Writer.WriteAsync(delta, timeoutCts.Token);
                    },
                    timeoutCts.Token);

                if (Interlocked.CompareExchange(ref emittedDelta, 0, 0) == 0 && !string.IsNullOrEmpty(finalText))
                {
                    await channel.Writer.WriteAsync(finalText, timeoutCts.Token);
                }

                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                var traceId = Guid.NewGuid().ToString("N");
                logger.LogExceptionWithTrace(ex, traceId);
                channel.Writer.TryComplete(new MeAiUtility.MultiProvider.Exceptions.TimeoutException(
                    "Codex App Server streaming request timed out.",
                    ProviderName,
                    runtime.TimeoutSeconds,
                    traceId,
                    ex));
            }
            catch (Exception ex)
            {
                if (ex is not MultiProviderException and not OperationCanceledException)
                {
                    var traceId = Guid.NewGuid().ToString("N");
                    logger.LogExceptionWithTrace(ex, traceId);
                    channel.Writer.TryComplete(new ProviderException("Failed to execute Codex App Server streaming request.", ProviderName, traceId, null, null, ex));
                    return;
                }

                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var delta in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            }

            await sessionTask;
        }
        finally
        {
            if (!timeoutCts.IsCancellationRequested)
            {
                timeoutCts.Cancel();
            }

            try
            {
                await sessionTask;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IProviderCapabilities) ? this : null;

    public void Dispose()
    {
    }

    private CodexRuntimeOptions BuildRuntimeOptions(ChatOptions? optionsArg)
    {
        if (optionsArg?.ResponseFormat is not null)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException(
                "ResponseFormat is not supported by CodexAppServer provider.",
                ProviderName,
                "ResponseFormat");
        }

        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg) ?? new ConversationExecutionOptions();
        var ext = ParseExtensions(optionsArg);

        var timeoutSeconds = execution.TimeoutSeconds
            ?? GetExtensionInt(ext, "codex.timeoutSeconds")
            ?? options.TimeoutSeconds;

        if (timeoutSeconds <= 0)
        {
            throw new InvalidRequestException("TimeoutSeconds must be greater than zero.", ProviderName);
        }

        var modelId = optionsArg?.ModelId
            ?? execution.ModelId
            ?? GetExtensionString(ext, "codex.modelId")
            ?? options.ModelId;

        var reasoningEffort = execution.ReasoningEffort is not null
            ? execution.ReasoningEffort.Value.ToString().ToLowerInvariant()
            : GetExtensionString(ext, "codex.reasoningEffort") ?? options.ReasoningEffort;

        var approvalPolicy = GetExtensionString(ext, "codex.approvalPolicy") ?? options.ApprovalPolicy;
        var sandboxMode = GetExtensionString(ext, "codex.sandboxMode") ?? options.SandboxMode;
        var networkAccess = GetExtensionBoolean(ext, "codex.networkAccess") ?? options.NetworkAccess;
        var autoApprove = GetExtensionBoolean(ext, "codex.autoApprove") ?? options.AutoApprove;
        var serviceName = GetExtensionString(ext, "codex.serviceName") ?? execution.ClientName ?? options.ServiceName;
        var summary = GetExtensionString(ext, "codex.summary") ?? options.Summary;
        var personality = GetExtensionString(ext, "codex.personality") ?? options.Personality;
        var workingDirectory = execution.WorkingDirectory ?? GetExtensionString(ext, "codex.workingDirectory") ?? options.WorkingDirectory;

        if (string.IsNullOrWhiteSpace(approvalPolicy))
        {
            throw new InvalidRequestException("ApprovalPolicy must be configured.", ProviderName);
        }

        if (string.IsNullOrWhiteSpace(sandboxMode))
        {
            throw new InvalidRequestException("SandboxMode must be configured.", ProviderName);
        }

        return new CodexRuntimeOptions(
            modelId,
            reasoningEffort,
            workingDirectory,
            approvalPolicy,
            sandboxMode,
            networkAccess,
            serviceName,
            summary,
            personality,
            autoApprove,
            timeoutSeconds,
            execution.ClientName ?? "MeAiUtility.MultiProvider",
            typeof(CodexAppServerChatClient).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            options.CaptureEventsForDiagnostics);
    }

    private static ExtensionParameters? ParseExtensions(ChatOptions? optionsArg)
    {
        if (optionsArg?.AdditionalProperties is null
            || !optionsArg.AdditionalProperties.TryGetValue("meai.extensions", out var raw)
            || raw is null)
        {
            return null;
        }

        if (raw is not ExtensionParameters ext)
        {
            throw new InvalidRequestException("ChatOptions meai.extensions must be ExtensionParameters.", ProviderName);
        }

        var disallowed = ext.GetAllForProvider("openai")
            .Concat(ext.GetAllForProvider("azure"))
            .Concat(ext.GetAllForProvider("copilot"))
            .ToArray();

        if (disallowed.Length > 0)
        {
            throw new InvalidRequestException("Unsupported extension prefix for provider.", ProviderName);
        }

        return ext;
    }

    private void EnsureTransportIsSupported()
    {
        if (!string.Equals(options.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException(
                "Only stdio transport is supported by CodexAppServer provider.",
                ProviderName,
                "Transport");
        }
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken, int timeoutSeconds)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }

    private static string? GetExtensionString(ExtensionParameters? ext, string key)
    {
        if (ext is null || !ext.TryGet<object>(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            string value => value,
            _ => throw new InvalidRequestException($"Extension '{key}' must be a string.", ProviderName),
        };
    }

    private static bool? GetExtensionBoolean(ExtensionParameters? ext, string key)
    {
        if (ext is null || !ext.TryGet<object>(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            bool value => value,
            _ => throw new InvalidRequestException($"Extension '{key}' must be a boolean.", ProviderName),
        };
    }

    private static int? GetExtensionInt(ExtensionParameters? ext, string key)
    {
        if (ext is null || !ext.TryGet<object>(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int value => value,
            long value when value is >= int.MinValue and <= int.MaxValue => (int)value,
            long => throw new InvalidRequestException($"Extension '{key}' must be within Int32 range.", ProviderName),
            _ => throw new InvalidRequestException($"Extension '{key}' must be an integer.", ProviderName),
        };
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
