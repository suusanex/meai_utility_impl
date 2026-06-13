extern alias GitHubCopilotSdk;

using System.Text.Json;
using System.Threading.Channels;
using MultiCodingAgentFacade.Core.Exceptions;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using CopilotSdk = GitHubCopilotSdk::GitHub.Copilot;

namespace MultiCodingAgentFacade.GitHubCopilot;

public sealed class GitHubCopilotSdkWrapper : ICopilotSdkWrapper, IDisposable, IAsyncDisposable
{
    private const string SdkTracePrefix = "[LoggerTraceSource]";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private readonly GitHubCopilotOptions options;
    private readonly ILogger<GitHubCopilotSdkWrapper> logger;
    private readonly ILogger copilotSdkLogger;
    private readonly SemaphoreSlim clientLock = new(1, 1);
    private readonly Func<CancellationToken, Task<IReadOnlyList<CopilotModelInfo>>>? listModelsCore;
    private readonly Func<CopilotSdkInvocation, CancellationToken, Task<CopilotSdkResponse>>? sendCore;
    private readonly Func<CopilotSdkInvocation, CancellationToken, IAsyncEnumerable<CopilotStreamingUpdate>>? sendStreamingCore;
    private CopilotSdk.CopilotClient? client;
    private bool disposed;

    public GitHubCopilotSdkWrapper(GitHubCopilotOptions options, ILogger<GitHubCopilotSdkWrapper> logger)
        : this(options, logger, null, null)
    {
    }

    internal GitHubCopilotSdkWrapper(
        GitHubCopilotOptions options,
        ILogger<GitHubCopilotSdkWrapper> logger,
        Func<CancellationToken, Task<IReadOnlyList<CopilotModelInfo>>>? listModelsCore,
        Func<CopilotSdkInvocation, CancellationToken, Task<CopilotSdkResponse>>? sendCore,
        Func<CopilotSdkInvocation, CancellationToken, IAsyncEnumerable<CopilotStreamingUpdate>>? sendStreamingCore = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.copilotSdkLogger = new CopilotSdkTraceLogger(this.logger);
        this.listModelsCore = listModelsCore;
        this.sendCore = sendCore;
        this.sendStreamingCore = sendStreamingCore;
    }

    public bool SupportsStreaming => sendStreamingCore is not null || sendCore is null;

    public async Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (listModelsCore is not null)
        {
            return await listModelsCore(cancellationToken).ConfigureAwait(false);
        }

        var sdkClient = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        var models = await sdkClient.ListModelsAsync(cancellationToken).ConfigureAwait(false);
        return
        [
            .. models.Select(static model => new CopilotModelInfo(
                model.Id,
                model.SupportedReasoningEfforts?.ToArray() ?? [],
                string.IsNullOrWhiteSpace(model.DefaultReasoningEffort) ? null : model.DefaultReasoningEffort))
        ];
    }

    public async Task<CopilotSdkResponse> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);

        var invocation = BuildInvocation(prompt, config, options);
        LogRequestStart(invocation, config);

        if (sendCore is not null)
        {
            logger.LogInformation("GitHub Copilot message send start. Stage=message send start; RequestId={RequestId}", config.RequestId ?? "(none)");
            var sendCoreResponse = await WaitWithHeartbeatAsync(sendCore(invocation, cancellationToken), "sendCore", invocation, config, cancellationToken).ConfigureAwait(false);
            LogResponseSummary(sendCoreResponse.Text);
            return sendCoreResponse;
        }

        var sdkClient = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("GitHub Copilot session creation start. Stage=session creation start; RequestId={RequestId}", config.RequestId ?? "(none)");
        await using var session = await sdkClient.CreateSessionAsync(BuildSdkSessionConfig(invocation), cancellationToken).ConfigureAwait(false);
        logger.LogDebug("GitHub Copilot session creation completed. Stage=session creation completed; RequestId={RequestId}", config.RequestId ?? "(none)");
        logger.LogInformation("GitHub Copilot message send start. Stage=message send start; RequestId={RequestId}", config.RequestId ?? "(none)");
        var response = await SendAndWaitAsync(session, invocation, config, cancellationToken).ConfigureAwait(false);

        var text = response?.Data?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("GitHub Copilot SDK returned no output.");
        }

        LogResponseSummary(text);

        return new CopilotSdkResponse(
            text,
            FinishStatus: "Completed",
            DiagnosticsSummary: $"GitHub Copilot SDK returned {text.Length} characters.",
            SdkMetadata: new Dictionary<string, object?>
            {
                ["sdk.response.contentLength"] = text.Length,
            });
    }

    public async IAsyncEnumerable<CopilotStreamingUpdate> SendStreamingAsync(
        string prompt,
        CopilotSessionConfig config,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);

        var invocation = BuildInvocation(prompt, config, options);
        LogRequestStart(invocation, config);

        if (sendStreamingCore is null && sendCore is not null)
        {
            throw new RuntimeFeatureNotSupportedException(
                "Streaming is not supported by the configured GitHubCopilot SDK wrapper.",
                "GitHubCopilot",
                "Streaming");
        }

        var seenFirstEvent = false;
        var seenFirstDelta = false;
        var updates = sendStreamingCore is not null
            ? sendStreamingCore(invocation, cancellationToken)
            : SendStreamingWithSdkAsync(invocation, config, cancellationToken);

        await foreach (var update in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!seenFirstEvent)
            {
                seenFirstEvent = true;
                logger.LogInformation("GitHub Copilot first SDK event received. Stage=first SDK event received; RequestId={RequestId}", config.RequestId ?? "(none)");
            }

            if (!seenFirstDelta && update.Kind == CopilotStreamingUpdateKind.Delta && !string.IsNullOrEmpty(update.TextDelta))
            {
                seenFirstDelta = true;
                logger.LogInformation("GitHub Copilot first response delta received. Stage=first response delta received; RequestId={RequestId}", config.RequestId ?? "(none)");
            }

            if (update.Kind == CopilotStreamingUpdateKind.Progress)
            {
                logger.LogDebug(
                    "GitHub Copilot streaming progress. Stage=response delta progress; DeltaCount={DeltaCount}; AccumulatedLength={AccumulatedLength}; RequestId={RequestId}",
                    update.DeltaCount,
                    update.AccumulatedLength,
                    config.RequestId ?? "(none)");
            }

            if (update.Kind == CopilotStreamingUpdateKind.Completed)
            {
                logger.LogInformation("GitHub Copilot final response received. Stage=final response received; RequestId={RequestId}", config.RequestId ?? "(none)");
            }

            yield return update;
        }
    }

    private async IAsyncEnumerable<CopilotStreamingUpdate> SendStreamingWithSdkAsync(
        CopilotSdkInvocation invocation,
        CopilotSessionConfig config,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sdkClient = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("GitHub Copilot session creation start. Stage=session creation start; RequestId={RequestId}", config.RequestId ?? "(none)");
        await using var session = await sdkClient.CreateSessionAsync(BuildSdkSessionConfig(invocation), cancellationToken).ConfigureAwait(false);
        logger.LogDebug("GitHub Copilot session creation completed. Stage=session creation completed; RequestId={RequestId}", config.RequestId ?? "(none)");
        logger.LogInformation("GitHub Copilot message send start. Stage=message send start; RequestId={RequestId}", config.RequestId ?? "(none)");

        var updates = Channel.CreateUnbounded<CopilotStreamingUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        var state = new StreamingState();

        using var subscription = session.On<CopilotSdk.SessionEvent>(evt =>
        {
            if (evt is null)
            {
                return;
            }

            var update = CreateStreamingUpdate(evt, state);
            if (update is not null)
            {
                updates.Writer.TryWrite(update);
            }
        });

        var sendTask = SendAndWaitAsync(session, invocation, config, cancellationToken);

        while (true)
        {
            while (updates.Reader.TryRead(out var buffered))
            {
                yield return buffered;
            }

            if (sendTask.IsCompleted)
            {
                break;
            }

            var waitTask = updates.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var completed = await Task.WhenAny(sendTask, waitTask).ConfigureAwait(false);
            if (completed == sendTask)
            {
                break;
            }

            await waitTask.ConfigureAwait(false);
        }

        var response = await sendTask.ConfigureAwait(false);

        while (updates.Reader.TryRead(out var trailing))
        {
            yield return trailing;
        }

        var text = state.FinalText ?? response?.Data?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (state.LastErrorMessage is not null)
            {
                throw new InvalidOperationException($"GitHub Copilot SDK returned no output. Session error: {state.LastErrorMessage}");
            }

            throw new InvalidOperationException("GitHub Copilot SDK returned no output.");
        }

        LogResponseSummary(text);
        yield return new CopilotStreamingUpdate(
            CopilotStreamingUpdateKind.Completed,
            FinalText: text,
            DeltaCount: state.DeltaCount,
            AccumulatedLength: state.AccumulatedLength,
            FinishStatus: "Completed",
            DiagnosticsSummary: BuildStreamingDiagnosticsSummary(text.Length, state),
            SdkMetadata: BuildStreamingMetadata(text.Length, state));
    }

    internal static CopilotStreamingUpdate? CreateStreamingUpdate(
        CopilotSdk.SessionEvent sessionEvent,
        StreamingState state)
    {
        switch (sessionEvent)
        {
            case CopilotSdk.AssistantMessageDeltaEvent messageDelta when !string.IsNullOrWhiteSpace(messageDelta.Data?.DeltaContent):
                var delta = messageDelta.Data.DeltaContent;
                state.DeltaCount++;
                state.AccumulatedLength += delta.Length;
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Delta,
                    TextDelta: delta,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength);
            case CopilotSdk.AssistantMessageEvent messageEvent:
                state.FinalText = messageEvent.Data?.Content?.Trim();
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Progress,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength,
                    SdkMetadata: BuildEventMetadata(messageEvent.Data?.RequestId, messageEvent.Data?.ServiceRequestId));
            case CopilotSdk.AssistantStreamingDeltaEvent streamingDelta:
                state.LastStreamingResponseSizeBytes = streamingDelta.Data?.TotalResponseSizeBytes;
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Progress,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength,
                    SdkMetadata: BuildEventMetadata(totalResponseSizeBytes: state.LastStreamingResponseSizeBytes));
            case CopilotSdk.AssistantReasoningDeltaEvent:
                state.ReasoningDeltaCount++;
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Progress,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength,
                    SdkMetadata: BuildEventMetadata(reasoningDeltaCount: state.ReasoningDeltaCount));
            case CopilotSdk.SessionErrorEvent sessionError:
                state.LastErrorMessage = sessionError.Data?.Message?.Trim();
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Progress,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength,
                    DiagnosticsSummary: state.LastErrorMessage,
                    SdkMetadata: BuildEventMetadata(
                        errorCode: sessionError.Data?.ErrorCode,
                        statusCode: sessionError.Data?.StatusCode,
                        serviceRequestId: sessionError.Data?.ServiceRequestId));
            default:
                return new CopilotStreamingUpdate(
                    CopilotStreamingUpdateKind.Progress,
                    DeltaCount: state.DeltaCount,
                    AccumulatedLength: state.AccumulatedLength);
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await clientLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            var sdkClient = Interlocked.Exchange(ref client, null);
            if (sdkClient is not null)
            {
                await sdkClient.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            clientLock.Release();
        }

        GC.SuppressFinalize(this);
    }

    internal static CopilotSdkInvocation BuildInvocation(string prompt, CopilotSessionConfig config, GitHubCopilotOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);

        var authOptions = ResolveClientAuthOptions(options);
        if (authOptions.UseLoggedInUser is false && string.IsNullOrWhiteSpace(authOptions.GitHubToken))
        {
            throw new InvalidOperationException("GitHubToken is required when UseLoggedInUser is false.");
        }

        ValidateRuntimeOptions(options);

        var mode = GetOptionalString(config.AdvancedOptions, "copilot.mode", "copilot.messageMode");
        var baseDirectory = GetOptionalString(config.AdvancedOptions, "copilot.baseDirectory", "copilot.base_directory") ?? options.BaseDirectory;
        var configDir = GetOptionalString(config.AdvancedOptions, "copilot.configDir", "copilot.config_dir") ?? options.ConfigDir;
        var workingDirectory = GetOptionalString(config.AdvancedOptions, "copilot.workingDirectory", "copilot.working_directory") ?? options.WorkingDirectory;
        var availableTools = GetOptionalStringList(config.AdvancedOptions, "copilot.availableTools", "copilot.available_tools") ?? options.AvailableTools?.ToArray();
        var excludedTools = GetOptionalStringList(config.AdvancedOptions, "copilot.excludedTools", "copilot.excluded_tools") ?? options.ExcludedTools?.ToArray();
        var mcpServers = GetOptionalDictionary(config.AdvancedOptions, "copilot.mcpServers", "copilot.mcp_servers");
        var agent = GetOptionalString(config.AdvancedOptions, "copilot.agent");
        var skillDirectories = config.SkillDirectories ?? GetOptionalStringList(config.AdvancedOptions, "copilot.skillDirectories", "copilot.skill_directories");
        var disabledSkills = config.DisabledSkills ?? GetOptionalStringList(config.AdvancedOptions, "copilot.disabledSkills", "copilot.disabled_skills");
        var timeoutSeconds = config.TimeoutSeconds ?? Math.Max(options.TimeoutSeconds, 1);
        if (timeoutSeconds <= 0)
        {
            throw new RuntimeInvalidRequestException("TimeoutSeconds must be greater than zero.", "GitHubCopilot");
        }

        ValidateAttachments(config.Attachments);

        EnsureSupportedAdvancedOptions(config.AdvancedOptions);

        return new CopilotSdkInvocation(
            prompt,
            mode,
            config.ModelId ?? options.ModelId,
            MapReasoningEffort(config.ReasoningEffort ?? options.ReasoningEffort),
            config.Streaming ?? options.Streaming ?? false,
            baseDirectory,
            configDir,
            workingDirectory,
            options.ClientName,
            availableTools,
            excludedTools,
            ValidateModelProvider(config.ModelProvider ?? options.ModelProvider),
            config.PermissionHandling,
            config.InfiniteSessions ?? options.InfiniteSessions,
            ValidateMcpServers(mcpServers),
            agent,
            skillDirectories,
            disabledSkills,
            config.Attachments?.ToArray(),
            timeoutSeconds);
    }

    internal static CopilotSdk.SessionConfig BuildSdkSessionConfig(CopilotSdkInvocation invocation)
    {
        return new CopilotSdk.SessionConfig
        {
            Model = invocation.ModelId,
            ReasoningEffort = invocation.ReasoningEffort,
            Streaming = invocation.Streaming,
            ConfigDirectory = invocation.ConfigDir,
            WorkingDirectory = invocation.WorkingDirectory,
            ClientName = invocation.ClientName,
            AvailableTools = invocation.AvailableTools?.ToList(),
            ExcludedTools = invocation.ExcludedTools?.ToList(),
            Provider = invocation.ModelProvider is null ? null : new CopilotSdk.ProviderConfig
            {
                Type = invocation.ModelProvider.Type!,
                BaseUrl = invocation.ModelProvider.BaseUrl!,
                ApiKey = invocation.ModelProvider.ApiKey,
                BearerToken = invocation.ModelProvider.BearerToken,
                Azure = string.IsNullOrWhiteSpace(invocation.ModelProvider.AzureApiVersion)
                    ? null
                    : new CopilotSdk.AzureOptions { ApiVersion = invocation.ModelProvider.AzureApiVersion },
            },
            InfiniteSessions = invocation.InfiniteSessions is null ? null : new CopilotSdk.InfiniteSessionConfig
            {
                Enabled = invocation.InfiniteSessions.Enabled,
                BackgroundCompactionThreshold = invocation.InfiniteSessions.BackgroundCompactionThreshold,
                BufferExhaustionThreshold = invocation.InfiniteSessions.BufferExhaustionThreshold,
            },
            McpServers = invocation.McpServers is null ? null : new Dictionary<string, CopilotSdk.McpServerConfig>(invocation.McpServers, StringComparer.Ordinal),
            Agent = invocation.Agent,
            SkillDirectories = invocation.SkillDirectories?.ToList(),
            DisabledSkills = invocation.DisabledSkills?.ToList(),
            OnPermissionRequest = BuildPermissionHandler(invocation.PermissionHandling),
        };
    }

    private async Task<CopilotSdk.CopilotClient> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var existing = client;
        if (existing is not null)
        {
            logger.LogDebug("GitHub Copilot client reuse completed. Stage=client reuse completed");
            return existing;
        }

        logger.LogDebug("GitHub Copilot client creation start. Stage=client creation start");
        await clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (client is not null)
            {
                logger.LogDebug("GitHub Copilot client reuse completed. Stage=client reuse completed");
                return client;
            }

            var authOptions = ResolveClientAuthOptions(options);
            try
            {
                var clientOptions = new CopilotSdk.CopilotClientOptions
                {
                    Connection = BuildRuntimeConnection(options),
                    WorkingDirectory = options.WorkingDirectory,
                    BaseDirectory = options.BaseDirectory,
                    LogLevel = MapLogLevel(options.LogLevel),
                    GitHubToken = authOptions.GitHubToken,
                    UseLoggedInUser = authOptions.UseLoggedInUser,
                    Environment = options.EnvironmentVariables,
                    Logger = copilotSdkLogger,
                };
                client = new CopilotSdk.CopilotClient(clientOptions);
                logger.LogDebug("GitHub Copilot client creation completed. Stage=client creation completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not CopilotRuntimeException)
            {
                logger.LogError(ex, "GitHub Copilot client creation failed. Stage=client creation failed; Exception={Exception}", ex.ToString());
                throw BuildClientInitializationException(ex);
            }

            return client;
        }
        finally
        {
            clientLock.Release();
        }
    }

    private async Task<CopilotSdk.AssistantMessageEvent> SendAndWaitAsync(
        CopilotSdk.CopilotSession session,
        CopilotSdkInvocation invocation,
        CopilotSessionConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await WaitWithHeartbeatAsync(
                session.SendAndWaitAsync(
                    new CopilotSdk.MessageOptions
                    {
                        Prompt = invocation.Prompt,
                        Attachments = BuildMessageAttachments(invocation.Attachments),
                        Mode = invocation.Mode,
                    },
                    TimeSpan.FromSeconds(invocation.TimeoutSeconds),
                    cancellationToken),
                "sendAndWait",
                invocation,
                config,
                cancellationToken)
                .ConfigureAwait(false);

            return response ?? throw new InvalidOperationException("GitHub Copilot SDK returned a null assistant message event.");
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            await AbortSessionSafelyAsync(session, config, ex).ConfigureAwait(false);
            throw;
        }
    }

    private async Task AbortSessionSafelyAsync(CopilotSdk.CopilotSession session, CopilotSessionConfig config, Exception originalException)
    {
        try
        {
            await session.AbortAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception abortException)
        {
            logger.LogWarning(
                "GitHub Copilot abort attempt failed. Stage=abort failed; RequestId={RequestId}; Exception={Exception}; OriginalException={OriginalException}",
                config.RequestId ?? "(none)",
                abortException.ToString(),
                originalException.ToString());
        }
    }

    private static void ValidateRuntimeOptions(GitHubCopilotOptions options)
    {
        if (!options.AutoStart)
        {
            throw new RuntimeInvalidRequestException("GitHubCopilotOptions.AutoStart=false is not supported with GitHub Copilot SDK 1.0.1.", "GitHubCopilot");
        }

        if (!options.AutoRestart)
        {
            throw new RuntimeInvalidRequestException("GitHubCopilotOptions.AutoRestart=false is not supported with GitHub Copilot SDK 1.0.1.", "GitHubCopilot");
        }
    }

    internal static CopilotSdk.RuntimeConnection BuildRuntimeConnection(GitHubCopilotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            return CopilotSdk.RuntimeConnection.ForUri(options.CliUrl.Trim(), options.ConnectionToken);
        }

        var cliArgs = options.CliArgs?.ToList();
        return options.UseStdio
            ? CopilotSdk.RuntimeConnection.ForStdio(options.CliPath, cliArgs)
            : CopilotSdk.RuntimeConnection.ForTcp(0, options.ConnectionToken, options.CliPath, cliArgs);
    }

    internal static string DescribeRuntimeConnection(GitHubCopilotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            return $"uri:{options.CliUrl.Trim()}";
        }

        return options.UseStdio
            ? $"stdio:{options.CliPath ?? "(bundled-runtime)"}"
            : $"tcp:{options.CliPath ?? "(bundled-runtime)"}";
    }

    internal static CopilotSdk.CopilotLogLevel? MapLogLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "none" => CopilotSdk.CopilotLogLevel.None,
            "error" => CopilotSdk.CopilotLogLevel.Error,
            "warning" => CopilotSdk.CopilotLogLevel.Warning,
            "info" => CopilotSdk.CopilotLogLevel.Info,
            "debug" => CopilotSdk.CopilotLogLevel.Debug,
            "all" => CopilotSdk.CopilotLogLevel.All,
            _ => throw new RuntimeInvalidRequestException($"GitHubCopilotOptions.LogLevel '{value}' is invalid. Valid values: none, error, warning, info, debug, all.", "GitHubCopilot"),
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    internal static bool TryTranslateSdkTraceMessage(string rawMessage, out LogLevel mappedLevel, out string? translatedMessage)
    {
        mappedLevel = LogLevel.Debug;
        translatedMessage = null;

        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return false;
        }

        var message = rawMessage;
        var tracePrefixIndex = message.IndexOf(SdkTracePrefix, StringComparison.Ordinal);
        if (tracePrefixIndex >= 0)
        {
            message = message[(tracePrefixIndex + SdkTracePrefix.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }
        }

        if (IsNoiseSessionEventTrace(message))
        {
            return false;
        }

        if (TryTranslateJsonRpcTrace(message, out mappedLevel, out translatedMessage))
        {
            return translatedMessage is not null;
        }

        translatedMessage = tracePrefixIndex >= 0
            ? $"Copilot SDK trace: {message}"
            : $"Copilot SDK: {message}";
        return true;
    }

    internal static CopilotClientAuthOptions ResolveClientAuthOptions(GitHubCopilotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            return new CopilotClientAuthOptions(options.GitHubToken, false);
        }

        return new CopilotClientAuthOptions(null, options.UseLoggedInUser);
    }

    private static bool TryTranslateJsonRpcTrace(string message, out LogLevel mappedLevel, out string? translatedMessage)
    {
        mappedLevel = LogLevel.Debug;
        translatedMessage = null;

        if (!message.StartsWith('{') || !message.EndsWith('}'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            var method = TryReadStringProperty(root, "method");
            var requestId = TryReadRequestId(root);
            if (string.Equals(method, "session.event", StringComparison.Ordinal))
            {
                if (TryExtractSessionEventText(root, out var streamText))
                {
                    mappedLevel = LogLevel.Debug;
                    translatedMessage = $"Copilot SDK stream text received. Length={streamText.Length}.";
                    return true;
                }

                translatedMessage = null;
                return true;
            }

            if (string.Equals(method, "session.permissions.handlePendingPermissionRequest", StringComparison.Ordinal))
            {
                mappedLevel = LogLevel.Information;
                translatedMessage = requestId is null
                    ? "Copilot SDK reported a pending permission request that was auto-approved."
                    : $"Copilot SDK reported a pending permission request that was auto-approved. RequestId={requestId}.";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(method))
            {
                mappedLevel = LogLevel.Debug;
                translatedMessage = requestId is null
                    ? $"Copilot SDK RPC call observed. Method={method}."
                    : $"Copilot SDK RPC call observed. Method={method}; RequestId={requestId}.";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                mappedLevel = LogLevel.Debug;
                translatedMessage = $"Copilot SDK RPC request completed. RequestId={requestId}.";
                return true;
            }

            return false;
        }
        catch (JsonException ex)
        {
            mappedLevel = LogLevel.Debug;
            translatedMessage = $"Copilot SDK trace parse failed. Exception={ex}";
            return true;
        }
    }

    private static bool TryExtractSessionEventText(JsonElement root, out string text)
    {
        if (root.TryGetProperty("params", out var @params) && TryFindMeaningfulText(@params, out text, 0))
        {
            return true;
        }

        if (root.TryGetProperty("result", out var result) && TryFindMeaningfulText(result, out text, 0))
        {
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryFindMeaningfulText(JsonElement element, out string text, int depth)
    {
        const int MaxDepth = 6;
        if (depth > MaxDepth)
        {
            text = string.Empty;
            return false;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var candidate = element.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    text = TruncateForLog(candidate, 300);
                    return true;
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (IsTextLikePropertyName(property.Name)
                        && TryFindMeaningfulText(property.Value, out text, depth + 1))
                    {
                        return true;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindMeaningfulText(property.Value, out text, depth + 1))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindMeaningfulText(item, out text, depth + 1))
                    {
                        return true;
                    }
                }

                break;
        }

        text = string.Empty;
        return false;
    }

    private static bool IsTextLikePropertyName(string name)
    {
        return string.Equals(name, "text", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "content", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "delta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "message", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "value", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "output", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoiseSessionEventTrace(string message)
    {
        if (string.Equals(message, "Received notification for method \"session.event\".", StringComparison.Ordinal))
        {
            return true;
        }

        return message.StartsWith(
            "Invoking GitHub.Copilot.SDK.CopilotClient+RpcHandler.session.event",
            StringComparison.Ordinal);
    }

    private static string? TryReadStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is not JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string? TryReadRequestId(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id))
        {
            return null;
        }

        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.GetRawText(),
            _ => null,
        };
    }

    private void LogRequestStart(CopilotSdkInvocation invocation, CopilotSessionConfig config)
    {
        logger.LogInformation(
            "Starting GitHub Copilot SDK request. Stage=request accepted; TraceId={TraceId}; RequestId={RequestId}; Model={ModelId}; Streaming={Streaming}; TimeoutSeconds={TimeoutSeconds}; AttachmentCount={AttachmentCount}; ModelProviderType={ModelProviderType}; ModelProviderBaseUrl={ModelProviderBaseUrl}; ModelProviderAzureApiVersion={ModelProviderAzureApiVersion}; ModelProviderHasApiKey={ModelProviderHasApiKey}; ModelProviderHasBearerToken={ModelProviderHasBearerToken}; PermissionHandling={PermissionHandling}",
            config.TraceId ?? "(none)",
            config.RequestId ?? "(none)",
            invocation.ModelId ?? "(default)",
            invocation.Streaming,
            invocation.TimeoutSeconds,
            invocation.Attachments?.Count ?? 0,
            invocation.ModelProvider?.Type,
            invocation.ModelProvider?.BaseUrl,
            invocation.ModelProvider?.AzureApiVersion,
            !string.IsNullOrWhiteSpace(invocation.ModelProvider?.ApiKey),
            !string.IsNullOrWhiteSpace(invocation.ModelProvider?.BearerToken),
            invocation.PermissionHandling);
    }

    private void LogResponseSummary(string responseText)
    {
        logger.LogInformation(
            "GitHub Copilot SDK request completed. CharacterCount={CharacterCount}",
            responseText.Length);

        if (options.EnableDiagnosticContentPreview && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "GitHub Copilot response preview: {Preview}",
                TruncateForLog(responseText.Trim(), options.DiagnosticContentPreviewLength));
        }
    }

    private async Task<T> WaitWithHeartbeatAsync<T>(Task<T> task, string stage, CopilotSdkInvocation invocation, CopilotSessionConfig config, CancellationToken cancellationToken)
    {
        var heartbeatCount = 0;

        try
        {
            while (!task.IsCompleted)
            {
                var delayTask = Task.Delay(HeartbeatInterval, cancellationToken);
                var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
                if (completed == task)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                heartbeatCount++;
                logger.LogDebug(
                    "GitHub Copilot waiting heartbeat. Stage={Stage}; HeartbeatCount={HeartbeatCount}; Model={ModelId}; TimeoutSeconds={TimeoutSeconds}; RequestId={RequestId}",
                    stage,
                    heartbeatCount,
                    invocation.ModelId ?? "(default)",
                    invocation.TimeoutSeconds,
                    config.RequestId ?? "(none)");
            }

            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("GitHub Copilot request cancelled. Stage=cancellation; RequestId={RequestId}; Exception={Exception}", config.RequestId ?? "(none)", ex.ToString());
            throw;
        }
        catch (System.TimeoutException ex)
        {
            logger.LogWarning(ex, "GitHub Copilot request timed out. Stage=timeout; TimeoutSeconds={TimeoutSeconds}; RequestId={RequestId}; Exception={Exception}", invocation.TimeoutSeconds, config.RequestId ?? "(none)", ex.ToString());
            throw;
        }
        catch (Exception ex) when (IsDisconnectedException(ex))
        {
            logger.LogError(ex, "GitHub Copilot SDK/CLI disconnected. Stage=disconnected; RequestId={RequestId}; Exception={Exception}", config.RequestId ?? "(none)", ex.ToString());
            throw;
        }
    }

    private static bool IsDisconnectedException(Exception exception)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("disconnect", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
            || message.Contains("eof", StringComparison.OrdinalIgnoreCase);
    }

    private static string TruncateForLog(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (maxLength <= 0)
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : $"{value[..maxLength]}...";
    }

    internal static bool TryLogSdkTrace(ILogger logger, LogLevel logLevel, EventId eventId, string rawMessage, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(rawMessage);

        if (exception is not null)
        {
            if (!logger.IsEnabled(logLevel))
            {
                return false;
            }

            logger.Log(logLevel, eventId, "Copilot SDK error: {Message}", rawMessage);
            logger.Log(logLevel, eventId, exception, "Copilot SDK exception captured.");
            return true;
        }

        if (!TryTranslateSdkTraceMessage(rawMessage, out var mappedLevel, out var translatedMessage)
            || string.IsNullOrWhiteSpace(translatedMessage)
            || !logger.IsEnabled(mappedLevel))
        {
            return false;
        }

        logger.Log(mappedLevel, eventId, "{Message}", translatedMessage);
        return true;
    }

    private sealed class CopilotSdkTraceLogger(ILogger logger) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logger.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var rawMessage = formatter(state, exception);
            _ = TryLogSdkTrace(logger, logLevel, eventId, rawMessage, exception);
        }
    }

    private CopilotRuntimeException BuildClientInitializationException(Exception ex)
    {
        var diagnostics = BuildCliDiagnosticsSummary();
        logger.LogWarning("Copilot CLI initialization failed. {Diagnostics}", diagnostics);
        logger.LogDebug("Copilot CLI resolution diagnostics detail: {Diagnostics}", BuildCliDiagnosticsDetail());
        var traceId = Guid.NewGuid().ToString("N");
        logger.LogExceptionWithTrace(ex, traceId);
        return new CopilotRuntimeException(
            $"Failed to initialize GitHub Copilot client. {diagnostics}",
            "GitHubCopilot",
            options.CliPath,
            null,
            DescribeRuntimeConnection(options),
            traceId,
            ex,
            CopilotOperation.ClientInitialization);
    }

    internal string BuildCliDiagnosticsSummary()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = GetPathEntries(path);
        var knownLocations = GetKnownCliLocations();
        return $"OS={Environment.OSVersion.VersionString}; CliPath={options.CliPath ?? "(not set)"}; RuntimeConnection={DescribeRuntimeConnection(options)}; BaseDirectory={options.BaseDirectory ?? "(not set)"}; PathEntryCount={pathEntries.Count}; KnownLocationCount={knownLocations.Count}";
    }

    internal string BuildCliDiagnosticsDetail()
    {
        var pathEntries = GetPathEntries(Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        var maskedPathPreview = string.Join("; ", pathEntries.Take(5).Select(MaskUserDirectory));
        var knownLocations = string.Join("; ", GetKnownCliLocations().Select(MaskUserDirectory));
        var preview = pathEntries.Count > 5
            ? $"{maskedPathPreview}; ... ({pathEntries.Count - 5} more)"
            : maskedPathPreview;

        return $"{BuildCliDiagnosticsSummary()}; PathPreview={preview}; KnownLocations={knownLocations}";
    }

    private static IReadOnlyList<string> GetKnownCliLocations()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return
            [
                Path.Combine(localAppData, "Programs", "GitHub Copilot", "copilot.exe"),
                Path.Combine(appData, "npm", "copilot.cmd"),
            ];
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            Path.Combine(homeDirectory, ".npm-global", "bin", "copilot"),
            "/usr/local/bin/copilot",
            "/opt/homebrew/bin/copilot",
        ];
    }

    private static List<CopilotSdk.Attachment>? BuildMessageAttachments(IReadOnlyList<FileAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return null;
        }

        return
        [
            .. attachments.Select(static attachment => (CopilotSdk.Attachment)CreateSdkFileAttachment(attachment)),
        ];
    }

    internal static IReadOnlyDictionary<string, object?> BuildStreamingMetadata(int contentLength, StreamingState state)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["sdk.response.contentLength"] = contentLength,
        };

        foreach (var item in BuildEventMetadata(
            reasoningDeltaCount: state.ReasoningDeltaCount,
            totalResponseSizeBytes: state.LastStreamingResponseSizeBytes,
            errorMessage: state.LastErrorMessage))
        {
            metadata[item.Key] = item.Value;
        }

        return metadata;
    }

    internal static string BuildStreamingDiagnosticsSummary(int contentLength, StreamingState state)
    {
        var summary = $"GitHub Copilot SDK returned {contentLength} characters.";
        if (state.ReasoningDeltaCount > 0)
        {
            summary += $" Reasoning delta events={state.ReasoningDeltaCount}.";
        }

        if (!string.IsNullOrWhiteSpace(state.LastErrorMessage))
        {
            summary += $" Last session error={state.LastErrorMessage}.";
        }

        return summary;
    }

    private static IReadOnlyDictionary<string, object?> BuildEventMetadata(
        string? requestId = null,
        string? serviceRequestId = null,
        string? errorCode = null,
        int? statusCode = null,
        long? totalResponseSizeBytes = null,
        int? reasoningDeltaCount = null,
        string? errorMessage = null)
    {
        var metadata = new Dictionary<string, object?>();
        AddIfNotNull(metadata, "sdk.requestId", requestId);
        AddIfNotNull(metadata, "sdk.serviceRequestId", serviceRequestId);
        AddIfNotNull(metadata, "sdk.errorCode", errorCode);
        AddIfNotNull(metadata, "sdk.statusCode", statusCode);
        AddIfNotNull(metadata, "sdk.totalResponseSizeBytes", totalResponseSizeBytes);
        AddIfNotNull(metadata, "sdk.reasoningDeltaCount", reasoningDeltaCount);
        AddIfNotNull(metadata, "sdk.lastSessionError", errorMessage);
        return metadata;
    }

    private static void AddIfNotNull(IDictionary<string, object?> target, string key, object? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }

    private static CopilotSdk.AttachmentFile CreateSdkFileAttachment(FileAttachment attachment)
    {
        var path = ValidateAttachmentPath(attachment);
        var displayName = string.IsNullOrWhiteSpace(attachment.DisplayName)
            ? Path.GetFileName(path)
            : attachment.DisplayName;

        var sdkAttachment = new CopilotSdk.AttachmentFile
        {
            Path = path,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? path : displayName,
        };

        return sdkAttachment;
    }

    private static void ValidateAttachments(IReadOnlyList<FileAttachment>? attachments)
    {
        if (attachments is null)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            _ = ValidateAttachmentPath(attachment);
        }
    }

    private static string ValidateAttachmentPath(FileAttachment? attachment)
    {
        if (attachment is null || string.IsNullOrWhiteSpace(attachment.Path))
        {
            throw new RuntimeInvalidRequestException("Attachment path must be specified.", "GitHubCopilot");
        }

        if (!Path.IsPathRooted(attachment.Path))
        {
            throw new RuntimeInvalidRequestException("Attachment path must be an absolute path.", "GitHubCopilot");
        }

        return attachment.Path;
    }

    private static IReadOnlyList<string> GetPathEntries(string path)
        => path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string MaskUserDirectory(string path)
    {
        foreach (var prefix in GetSensitivePathPrefixes())
        {
            if (!string.IsNullOrWhiteSpace(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var trimmedPrefix = prefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var marker = Path.GetFileName(trimmedPrefix);
                // ルート直下まで trim された場合は空文字になり得るため、ログ上の識別子を固定値へフォールバックする。
                if (string.IsNullOrWhiteSpace(marker))
                {
                    marker = "UserDir";
                }

                return $"<{marker}>{path[prefix.Length..]}";
            }
        }

        return path;
    }

    private static IReadOnlyList<string> GetSensitivePathPrefixes()
    {
        var prefixes = new List<string>();
        AddPrefix(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AddPrefix(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddPrefix(prefixes, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        return prefixes;
    }

    private static void AddPrefix(List<string> prefixes, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        prefixes.Add(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static void EnsureSupportedAdvancedOptions(IReadOnlyDictionary<string, object?> advancedOptions)
    {
        foreach (var key in advancedOptions.Keys)
        {
            if (SupportedAdvancedOptions.Contains(key))
            {
                continue;
            }

            throw new RuntimeInvalidRequestException($"Advanced option '{key}' is not supported by this SDK wrapper.", "GitHubCopilot");
        }
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            throw new RuntimeInvalidRequestException($"Advanced option '{key}' must be a non-empty string.", "GitHubCopilot");
        }

        return null;
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

            var parsed = DeserializeAdvancedOption<string[]>(value, key, "an array of strings");
            if (parsed is { Length: > 0 })
            {
                return parsed;
            }

            throw new RuntimeInvalidRequestException($"Advanced option '{key}' must be an array of strings.", "GitHubCopilot");
        }

        return null;
    }

    private static IReadOnlyDictionary<string, object>? GetOptionalDictionary(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is IReadOnlyDictionary<string, object> typed)
            {
                return typed;
            }

            if (value is IDictionary<string, object> dict)
            {
                return new Dictionary<string, object>(dict, StringComparer.Ordinal);
            }

            var parsed = DeserializeAdvancedOption<Dictionary<string, object>>(value, key, "an object");
            if (parsed is { Count: > 0 })
            {
                return parsed;
            }

            throw new RuntimeInvalidRequestException($"Advanced option '{key}' must be an object.", "GitHubCopilot");
        }

        return null;
    }

    private static string? GetRequiredString(IReadOnlyDictionary<string, object> values, string key, string label)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            throw new RuntimeInvalidRequestException($"{label} must be specified.", "GitHubCopilot");
        }

        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        throw new RuntimeInvalidRequestException($"{label} must be a non-empty string.", "GitHubCopilot");
    }

    private static IReadOnlyList<string>? GetOptionalStringArray(IReadOnlyDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is IEnumerable<string> typed)
        {
            return typed.ToArray();
        }

        var parsed = DeserializeAdvancedOption<string[]>(value, key, "an array of strings");
        if (parsed is not null)
        {
            return parsed;
        }

        throw new RuntimeInvalidRequestException($"MCP option '{key}' must be an array of strings.", "GitHubCopilot");
    }

    private static string? GetOptionalStringValue(IReadOnlyDictionary<string, object> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            throw new RuntimeInvalidRequestException($"MCP option '{key}' must be a non-empty string.", "GitHubCopilot");
        }

        return null;
    }

    private static Dictionary<string, string>? GetOptionalStringDictionary(IReadOnlyDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, string> typed)
        {
            return new Dictionary<string, string>(typed, StringComparer.Ordinal);
        }

        if (value is IDictionary<string, string> dict)
        {
            return new Dictionary<string, string>(dict, StringComparer.Ordinal);
        }

        var parsed = DeserializeAdvancedOption<Dictionary<string, string>>(value, key, "an object with string values");
        if (parsed is not null)
        {
            return parsed;
        }

        throw new RuntimeInvalidRequestException($"MCP option '{key}' must be an object with string values.", "GitHubCopilot");
    }

    private static int? GetOptionalInt32(IReadOnlyDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        var parsed = DeserializeAdvancedOption<int?>(value, key, "an integer");
        if (parsed.HasValue)
        {
            return parsed.Value;
        }

        throw new RuntimeInvalidRequestException($"MCP option '{key}' must be an integer.", "GitHubCopilot");
    }

    private static bool? GetOptionalBool(IReadOnlyDictionary<string, object> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            var parsed = DeserializeAdvancedOption<bool?>(value, key, "a boolean");
            if (parsed.HasValue)
            {
                return parsed.Value;
            }

            throw new RuntimeInvalidRequestException($"MCP option '{key}' must be a boolean.", "GitHubCopilot");
        }

        return null;
    }

    private static CopilotSdk.McpHttpServerConfigOauthGrantType? GetOptionalGrantType(IReadOnlyDictionary<string, object> values)
    {
        var value = GetOptionalStringValue(values, "oauthGrantType", "oauth_grant_type");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "authorizationcode" or "authorization_code" or "authorization-code" => CopilotSdk.McpHttpServerConfigOauthGrantType.AuthorizationCode,
            "clientcredentials" or "client_credentials" or "client-credentials" => CopilotSdk.McpHttpServerConfigOauthGrantType.ClientCredentials,
            _ => throw new RuntimeInvalidRequestException($"MCP option 'oauthGrantType' has unsupported value '{value}'. Supported values: authorization_code, client_credentials.", "GitHubCopilot"),
        };
    }

    private static string? MapReasoningEffort(ReasoningEffortLevel? reasoningEffort)
    {
        return reasoningEffort switch
        {
            null => null,
            ReasoningEffortLevel.Low => "low",
            ReasoningEffortLevel.Medium => "medium",
            ReasoningEffortLevel.High => "high",
            ReasoningEffortLevel.XHigh => "xhigh",
            _ => throw new RuntimeInvalidRequestException($"Unsupported reasoning effort '{reasoningEffort}'.", "GitHubCopilot"),
        };
    }

    private static GitHubCopilotModelProviderOptions? ValidateModelProvider(GitHubCopilotModelProviderOptions? modelProvider)
    {
        if (modelProvider is null)
        {
            return null;
        }

        var type = NormalizeRequired(modelProvider.Type, "ModelProvider.Type");
        if (!string.Equals(type, "openai", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(type, "azure", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeInvalidRequestException("ModelProvider.Type must be 'openai' or 'azure'.", "GitHubCopilot");
        }

        var baseUrl = NormalizeRequired(modelProvider.BaseUrl, "ModelProvider.BaseUrl");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new RuntimeInvalidRequestException("ModelProvider.BaseUrl must be an absolute HTTP or HTTPS URL.", "GitHubCopilot");
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(modelProvider.ApiKey);
        var hasBearerToken = !string.IsNullOrWhiteSpace(modelProvider.BearerToken);
        if (hasApiKey == hasBearerToken)
        {
            throw new RuntimeInvalidRequestException("ModelProvider must specify exactly one of ApiKey or BearerToken.", "GitHubCopilot");
        }

        var isAzure = string.Equals(type, "azure", StringComparison.OrdinalIgnoreCase);
        if (isAzure && string.IsNullOrWhiteSpace(modelProvider.AzureApiVersion))
        {
            throw new RuntimeInvalidRequestException("ModelProvider.AzureApiVersion must be specified when ModelProvider.Type is 'azure'.", "GitHubCopilot");
        }

        if (!isAzure && !string.IsNullOrWhiteSpace(modelProvider.AzureApiVersion))
        {
            throw new RuntimeInvalidRequestException("ModelProvider.AzureApiVersion is supported only when ModelProvider.Type is 'azure'.", "GitHubCopilot");
        }

        return new GitHubCopilotModelProviderOptions
        {
            Type = type,
            BaseUrl = baseUrl,
            ApiKey = string.IsNullOrWhiteSpace(modelProvider.ApiKey) ? null : modelProvider.ApiKey,
            BearerToken = string.IsNullOrWhiteSpace(modelProvider.BearerToken) ? null : modelProvider.BearerToken,
            AzureApiVersion = string.IsNullOrWhiteSpace(modelProvider.AzureApiVersion) ? null : modelProvider.AzureApiVersion,
        };
    }

    private static string NormalizeRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RuntimeInvalidRequestException($"{name} must be specified.", "GitHubCopilot");
        }

        return value.Trim();
    }

    internal static IReadOnlyDictionary<string, CopilotSdk.McpServerConfig>? ValidateMcpServers(IReadOnlyDictionary<string, object>? servers)
    {
        if (servers is null || servers.Count == 0)
        {
            return null;
        }

        var mapped = new Dictionary<string, CopilotSdk.McpServerConfig>(StringComparer.Ordinal);
        foreach (var (name, rawConfig) in servers)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new RuntimeInvalidRequestException("MCP server name must be specified.", "GitHubCopilot");
            }

            IReadOnlyDictionary<string, object> config;
            if (rawConfig is IReadOnlyDictionary<string, object> typedConfig)
            {
                config = typedConfig;
            }
            else if (rawConfig is IDictionary<string, object> dict)
            {
                config = new Dictionary<string, object>(dict, StringComparer.Ordinal);
            }
            else
            {
                config = DeserializeAdvancedOption<Dictionary<string, object>>(rawConfig, $"copilot.mcpServers.{name}", "an object")
                    ?? throw new RuntimeInvalidRequestException($"MCP server '{name}' must be an object.", "GitHubCopilot");
            }

            mapped[name] = BuildMcpServerConfig(name, config);
        }

        return mapped;
    }

    private static CopilotSdk.McpServerConfig BuildMcpServerConfig(string name, IReadOnlyDictionary<string, object> config)
    {
        var type = NormalizeRequired(GetRequiredString(config, "type", $"MCP server '{name}' type"), $"MCP server '{name}' type")
            .ToLowerInvariant();
        var tools = GetOptionalStringArray(config, "tools");
        var timeout = GetOptionalInt32(config, "timeout");

        return type switch
        {
            "stdio" => new CopilotSdk.McpStdioServerConfig
            {
                Command = NormalizeRequired(GetRequiredString(config, "command", $"MCP stdio server '{name}' command"), $"MCP stdio server '{name}' command"),
                Args = GetOptionalStringArray(config, "args")?.ToList(),
                Env = GetOptionalStringDictionary(config, "env"),
                WorkingDirectory = GetOptionalStringValue(config, "cwd", "workingDirectory", "working_directory"),
                Tools = tools?.ToList(),
                Timeout = timeout,
            },
            "http" => new CopilotSdk.McpHttpServerConfig
            {
                Url = NormalizeRequired(GetRequiredString(config, "url", $"MCP http server '{name}' url"), $"MCP http server '{name}' url"),
                Headers = GetOptionalStringDictionary(config, "headers"),
                OauthClientId = GetOptionalStringValue(config, "oauthClientId", "oauth_client_id"),
                OauthPublicClient = GetOptionalBool(config, "oauthPublicClient", "oauth_public_client"),
                OauthGrantType = GetOptionalGrantType(config),
                Tools = tools?.ToList(),
                Timeout = timeout,
            },
            _ => throw new RuntimeInvalidRequestException($"MCP server '{name}' has unsupported type '{type}'. Supported types: stdio, http.", "GitHubCopilot"),
        };
    }

    internal static Func<CopilotSdk.PermissionRequest, CopilotSdk.PermissionInvocation, Task<CopilotSdk.Rpc.PermissionDecision>> BuildPermissionHandler(GitHubCopilotPermissionHandlingMode mode)
    {
        return mode switch
        {
            GitHubCopilotPermissionHandlingMode.ApproveAll => CopilotSdk.PermissionHandler.ApproveAll,
            GitHubCopilotPermissionHandlingMode.DenyAll => static (_, _) => Task.FromResult(CopilotSdk.Rpc.PermissionDecision.Reject("Denied by MultiCodingAgentFacade policy.")),
            GitHubCopilotPermissionHandlingMode.NoResult => static (_, _) => Task.FromResult(CopilotSdk.Rpc.PermissionDecision.NoResult()),
            _ => throw new RuntimeInvalidRequestException($"Unsupported permission handling mode '{mode}'.", "GitHubCopilot"),
        };
    }

    private static T? DeserializeAdvancedOption<T>(object value, string key, string expectedType)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        }
        catch (JsonException ex)
        {
            throw CreateAdvancedOptionTypeException(key, expectedType, ex);
        }
    }

    private static RuntimeInvalidRequestException CreateAdvancedOptionTypeException(string key, string expectedType, Exception innerException)
    {
        return new RuntimeInvalidRequestException($"Advanced option '{key}' must be {expectedType}.", "GitHubCopilot", innerException: innerException);
    }

    private static readonly HashSet<string> SupportedAdvancedOptions = new(StringComparer.Ordinal)
    {
        "copilot.mode",
        "copilot.messageMode",
        "copilot.baseDirectory",
        "copilot.base_directory",
        "copilot.configDir",
        "copilot.config_dir",
        "copilot.workingDirectory",
        "copilot.working_directory",
        "copilot.availableTools",
        "copilot.available_tools",
        "copilot.excludedTools",
        "copilot.excluded_tools",
        "copilot.mcpServers",
        "copilot.mcp_servers",
        "copilot.agent",
        "copilot.skillDirectories",
        "copilot.skill_directories",
        "copilot.disabledSkills",
        "copilot.disabled_skills",
    };
}

internal sealed record CopilotSdkInvocation(
    string Prompt,
    string? Mode,
    string? ModelId,
    string? ReasoningEffort,
    bool Streaming,
    string? BaseDirectory,
    string? ConfigDir,
    string? WorkingDirectory,
    string? ClientName,
    IReadOnlyList<string>? AvailableTools,
    IReadOnlyList<string>? ExcludedTools,
    GitHubCopilotModelProviderOptions? ModelProvider,
    GitHubCopilotPermissionHandlingMode PermissionHandling,
    InfiniteSessionOptions? InfiniteSessions,
    IReadOnlyDictionary<string, CopilotSdk.McpServerConfig>? McpServers,
    string? Agent,
    IReadOnlyList<string>? SkillDirectories,
    IReadOnlyList<string>? DisabledSkills,
    IReadOnlyList<FileAttachment>? Attachments,
    int TimeoutSeconds);

internal sealed record CopilotClientAuthOptions(
    string? GitHubToken,
    bool? UseLoggedInUser);

internal sealed class StreamingState
{
    public int DeltaCount { get; set; }
    public int AccumulatedLength { get; set; }
    public int ReasoningDeltaCount { get; set; }
    public long? LastStreamingResponseSizeBytes { get; set; }
    public string? FinalText { get; set; }
    public string? LastErrorMessage { get; set; }
}
