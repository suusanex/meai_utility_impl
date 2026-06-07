using System.Threading.Channels;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.Core.Diagnostics;
using MultiCodingAgentFacade.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace MultiCodingAgentFacade.CodexAppServer;

public sealed class CodexAppServerAgentClient(
    CodexAppServerOptions options,
    ICodexTransportFactory transportFactory,
    ICodexThreadStore threadStore,
    ILogger<CodexAppServerAgentClient> logger,
    ILoggerFactory loggerFactory)
{
    private const string RuntimeName = CodexAppServerRuntimeMarker.RuntimeName;

    public async Task<CodexAppServerTurnResponse> ExecuteTurnAsync(
        CodexAppServerTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureTransportIsSupported();

        var runtime = BuildRuntimeOptions(request);
        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken, runtime.TimeoutSeconds);

        try
        {
            var transport = transportFactory.Create(runtime.WorkingDirectory);
            var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
            var session = new CodexRpcSession(transport, threadStore, sessionLogger);
            var text = await session.ExecuteTurnAsync(request.Prompt, runtime, onDelta: null, timeoutCts.Token);
            return new CodexAppServerTurnResponse(text);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new RuntimeTimeoutException("Codex App Server request timed out.", RuntimeName, runtime.TimeoutSeconds, traceId, ex);
        }
        catch (Exception ex) when (ex is not RuntimeFacadeException and not OperationCanceledException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new RuntimeOperationException("Failed to execute Codex App Server request.", RuntimeName, traceId, null, null, ex);
        }
    }

    public async IAsyncEnumerable<CodexAppServerStreamingUpdate> StreamTurnAsync(
        CodexAppServerTurnRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureTransportIsSupported();

        var runtime = BuildRuntimeOptions(request);
        using var timeoutCts = CreateTimeoutTokenSource(cancellationToken, runtime.TimeoutSeconds);
        var channel = Channel.CreateUnbounded<string>();
        var emittedDelta = 0;

        var sessionTask = Task.Run(async () =>
        {
            try
            {
                var transport = transportFactory.Create(runtime.WorkingDirectory);
                var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
                var session = new CodexRpcSession(transport, threadStore, sessionLogger);
                var finalText = await session.ExecuteTurnAsync(
                    request.Prompt,
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
                channel.Writer.TryComplete(new RuntimeTimeoutException(
                    "Codex App Server streaming request timed out.",
                    RuntimeName,
                    runtime.TimeoutSeconds,
                    traceId,
                    ex));
            }
            catch (Exception ex)
            {
                if (ex is not RuntimeFacadeException and not OperationCanceledException)
                {
                    var traceId = Guid.NewGuid().ToString("N");
                    logger.LogExceptionWithTrace(ex, traceId);
                    channel.Writer.TryComplete(new RuntimeOperationException("Failed to execute Codex App Server streaming request.", RuntimeName, traceId, null, null, ex));
                    return;
                }

                logger.LogError("Codex App Server streaming request failed. Exception={Exception}", ex.ToString());
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var delta in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return new CodexAppServerStreamingUpdate(CodexAppServerStreamingUpdateKind.Delta, TextDelta: delta);
            }

            await sessionTask;
            yield return new CodexAppServerStreamingUpdate(CodexAppServerStreamingUpdateKind.Completed);
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
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogDebug("Codex streaming cleanup was canceled. Exception={Exception}", ex.ToString());
            }
        }
    }

    private CodexRuntimeOptions BuildRuntimeOptions(CodexAppServerTurnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new RuntimeInvalidRequestException("Prompt must be specified.", RuntimeName);
        }

        var timeoutSeconds = request.TimeoutSeconds ?? options.TimeoutSeconds;
        if (timeoutSeconds <= 0)
        {
            throw new RuntimeInvalidRequestException("TimeoutSeconds must be greater than zero.", RuntimeName);
        }

        var threadReusePolicy = request.ThreadReusePolicy ?? options.ThreadReusePolicy;
        var threadId = NormalizeOptionalString(request.ThreadId ?? options.ThreadId);
        var threadKey = NormalizeOptionalString(request.ThreadKey ?? options.ThreadKey);
        ValidateThreadReuseOptions(threadReusePolicy, threadId, threadKey);

        return new CodexRuntimeOptions(
            NormalizeOptionalString(request.ModelId ?? options.ModelId),
            FormatReasoningEffort(request.ReasoningEffort ?? options.ReasoningEffort),
            NormalizeOptionalString(request.WorkingDirectory ?? options.WorkingDirectory),
            threadReusePolicy,
            threadId,
            threadKey,
            NormalizeOptionalString(request.ThreadName ?? options.ThreadName),
            NormalizeOptionalString(request.ThreadStorePath ?? options.ThreadStorePath),
            NormalizeApprovalPolicy(request.ApprovalPolicy ?? options.ApprovalPolicy),
            NormalizeSandboxMode(request.SandboxMode ?? options.SandboxMode),
            request.NetworkAccess ?? options.NetworkAccess,
            NormalizeOptionalString(request.ServiceName ?? options.ServiceName),
            NormalizeSummary(request.Summary ?? options.Summary),
            NormalizePersonality(request.Personality ?? options.Personality),
            request.AutoApprove ?? options.AutoApprove,
            timeoutSeconds,
            RuntimeName,
            typeof(CodexAppServerAgentClient).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            request.CaptureEventsForDiagnostics ?? options.CaptureEventsForDiagnostics);
    }

    private void EnsureTransportIsSupported()
    {
        if (!string.Equals(options.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeFeatureNotSupportedException(
                "Only stdio transport is supported by CodexAppServer runtime.",
                RuntimeName,
                "Transport");
        }
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken, int timeoutSeconds)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }

    private static string FormatReasoningEffort(CodexReasoningEffort effort) => effort switch
    {
        CodexReasoningEffort.None => "none",
        CodexReasoningEffort.Minimal => "minimal",
        CodexReasoningEffort.Low => "low",
        CodexReasoningEffort.Medium => "medium",
        CodexReasoningEffort.High => "high",
        CodexReasoningEffort.XHigh => "xhigh",
        _ => throw new RuntimeInvalidRequestException($"Unsupported Codex reasoning effort '{effort}'.", RuntimeName),
    };

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
            throw new RuntimeInvalidRequestException($"{optionName} must be configured.", RuntimeName);
        }

        return NormalizeOptionValue(value, optionName, canonicalValues)
            ?? throw new RuntimeInvalidRequestException($"{optionName} must be configured.", RuntimeName);
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
        throw new RuntimeInvalidRequestException($"{optionName} must be one of: {allowedValues}.", RuntimeName);
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
                throw new RuntimeInvalidRequestException("ThreadId must be configured when ThreadReusePolicy is ReuseByThreadId.", RuntimeName);
            case CodexThreadReusePolicy.ReuseOrCreateByKey when threadKey is null:
                throw new RuntimeInvalidRequestException("ThreadKey must be configured when ThreadReusePolicy is ReuseOrCreateByKey.", RuntimeName);
            default:
                return;
        }
    }
}
