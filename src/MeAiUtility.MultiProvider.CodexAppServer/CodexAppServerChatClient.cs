using System.Threading.Channels;
using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.CodexAppServer.Threading;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer;

public sealed class CodexAppServerChatClient : IChatClient, IProviderCapabilities
{
    private const string ProviderName = "CodexAppServer";
    private readonly CodexAppServerProviderOptions options;
    private readonly ICodexTransportFactory transportFactory;
    private readonly ICodexThreadStore threadStore;
    private readonly ILogger<CodexAppServerChatClient> logger;
    private readonly ILoggerFactory loggerFactory;

    public CodexAppServerChatClient(
        CodexAppServerProviderOptions options,
        ICodexTransportFactory transportFactory,
        ILogger<CodexAppServerChatClient> logger,
        ILoggerFactory loggerFactory)
        : this(
            options,
            transportFactory,
            new FileCodexThreadStore(options, loggerFactory.CreateLogger<FileCodexThreadStore>()),
            logger,
            loggerFactory)
    {
    }

    internal CodexAppServerChatClient(
        CodexAppServerProviderOptions options,
        ICodexTransportFactory transportFactory,
        ICodexThreadStore threadStore,
        ILogger<CodexAppServerChatClient> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transportFactory);
        ArgumentNullException.ThrowIfNull(threadStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.options = options;
        this.transportFactory = transportFactory;
        this.threadStore = threadStore;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
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
            var session = new CodexRpcSession(transport, threadStore, sessionLogger);
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
                var session = new CodexRpcSession(transport, threadStore, sessionLogger);
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
            ? NormalizeReasoningEffort(execution.ReasoningEffort.Value.ToString())
            : NormalizeReasoningEffort(GetExtensionString(ext, "codex.reasoningEffort") ?? options.ReasoningEffort);

        var approvalPolicy = NormalizeApprovalPolicy(GetExtensionString(ext, "codex.approvalPolicy") ?? options.ApprovalPolicy);
        var sandboxMode = NormalizeSandboxMode(GetExtensionString(ext, "codex.sandboxMode") ?? options.SandboxMode);
        var networkAccess = GetExtensionBoolean(ext, "codex.networkAccess") ?? options.NetworkAccess;
        var autoApprove = GetExtensionBoolean(ext, "codex.autoApprove") ?? options.AutoApprove;
        var serviceName = GetExtensionString(ext, "codex.serviceName") ?? execution.ClientName ?? options.ServiceName;
        var summary = NormalizeSummary(GetExtensionString(ext, "codex.summary") ?? options.Summary);
        var personality = NormalizePersonality(GetExtensionString(ext, "codex.personality") ?? options.Personality);
        var workingDirectory = execution.WorkingDirectory ?? GetExtensionString(ext, "codex.workingDirectory") ?? options.WorkingDirectory;
        var threadReusePolicy = ParseThreadReusePolicy(GetExtensionString(ext, "codex.threadReusePolicy"), options.ThreadReusePolicy);
        var threadId = NormalizeOptionalString(GetExtensionString(ext, "codex.threadId") ?? options.ThreadId);
        var threadKey = NormalizeOptionalString(GetExtensionString(ext, "codex.threadKey") ?? options.ThreadKey);
        var threadName = NormalizeOptionalString(GetExtensionString(ext, "codex.threadName") ?? options.ThreadName);
        var threadStorePath = NormalizeOptionalString(GetExtensionString(ext, "codex.threadStorePath") ?? options.ThreadStorePath);
        ValidateThreadReuseOptions(threadReusePolicy, threadId, threadKey);

        return new CodexRuntimeOptions(
            modelId,
            reasoningEffort,
            workingDirectory,
            threadReusePolicy,
            threadId,
            threadKey,
            threadName,
            threadStorePath,
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

    private static string NormalizeReasoningEffort(string? value)
        => NormalizeRequiredOptionValue(
            value,
            "ReasoningEffort",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["none"] = "none",
                ["minimal"] = "minimal",
                ["low"] = "low",
                ["medium"] = "medium",
                ["high"] = "high",
                ["xhigh"] = "xhigh",
                ["extra-high"] = "xhigh",
                ["extra_high"] = "xhigh",
                ["extrahigh"] = "xhigh",
            });

    private static string NormalizeApprovalPolicy(string value)
        => NormalizeRequiredOptionValue(
            value,
            "ApprovalPolicy",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["never"] = "never",
                ["on-request"] = "on-request",
                ["on_request"] = "on-request",
                ["onrequest"] = "on-request",
                ["on-failure"] = "on-failure",
                ["on_failure"] = "on-failure",
                ["onfailure"] = "on-failure",
                ["untrusted"] = "untrusted",
            });

    private static string NormalizeSandboxMode(string value)
        => NormalizeRequiredOptionValue(
            value,
            "SandboxMode",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["read-only"] = "read-only",
                ["readonly"] = "read-only",
                ["read_only"] = "read-only",
                ["workspace-write"] = "workspace-write",
                ["workspace_write"] = "workspace-write",
                ["workspacewrite"] = "workspace-write",
                ["danger-full-access"] = "danger-full-access",
                ["danger_full_access"] = "danger-full-access",
                ["dangerfullaccess"] = "danger-full-access",
            });

    private static string? NormalizeSummary(string? value)
        => NormalizeOptionValue(
            value,
            "Summary",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["auto"] = "auto",
                ["concise"] = "concise",
                ["detailed"] = "detailed",
                ["none"] = "none",
            });

    private static string? NormalizePersonality(string? value)
        => NormalizeOptionValue(
            value,
            "Personality",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["none"] = "none",
                ["friendly"] = "friendly",
                ["pragmatic"] = "pragmatic",
            });

    private static string NormalizeRequiredOptionValue(string? value, string optionName, IReadOnlyDictionary<string, string> canonicalValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRequestException($"{optionName} must be configured.", ProviderName);
        }

        return NormalizeOptionValue(value, optionName, canonicalValues)
            ?? throw new InvalidRequestException($"{optionName} must be configured.", ProviderName);
    }

    private static string? NormalizeOptionValue(string? value, string optionName, IReadOnlyDictionary<string, string> canonicalValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (canonicalValues.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        var allowedValues = string.Join(", ", canonicalValues.Values.Distinct(StringComparer.Ordinal));
        throw new InvalidRequestException($"{optionName} must be one of: {allowedValues}.", ProviderName);
    }

    private static CodexThreadReusePolicy ParseThreadReusePolicy(string? rawValue, CodexThreadReusePolicy fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "alwaysnew" or "always-new" or "always_new" => CodexThreadReusePolicy.AlwaysNew,
            "reusebythreadid" or "reuse-by-thread-id" or "reuse_by_thread_id" => CodexThreadReusePolicy.ReuseByThreadId,
            "reuseorcreatebykey" or "reuse-or-create-by-key" or "reuse_or_create_by_key" => CodexThreadReusePolicy.ReuseOrCreateByKey,
            _ => throw new InvalidRequestException(
                "ThreadReusePolicy must be one of: alwaysNew, reuseByThreadId, reuseOrCreateByKey.",
                ProviderName),
        };
    }

    private static string? NormalizeOptionalString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateThreadReuseOptions(CodexThreadReusePolicy policy, string? threadId, string? threadKey)
    {
        switch (policy)
        {
            case CodexThreadReusePolicy.AlwaysNew:
                return;
            case CodexThreadReusePolicy.ReuseByThreadId when threadId is null:
                throw new InvalidRequestException("ThreadId must be configured when ThreadReusePolicy is ReuseByThreadId.", ProviderName);
            case CodexThreadReusePolicy.ReuseOrCreateByKey when threadKey is null:
                throw new InvalidRequestException("ThreadKey must be configured when ThreadReusePolicy is ReuseOrCreateByKey.", ProviderName);
            default:
                return;
        }
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
