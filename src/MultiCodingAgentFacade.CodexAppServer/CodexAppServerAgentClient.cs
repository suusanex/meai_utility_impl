using System.Text.Json;
using System.Threading.Channels;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.Core.Diagnostics;
using MultiCodingAgentFacade.Core.Options;
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
        var (activity, telemetry) = AgentTelemetry.Start(RuntimeName, runtime.ModelId, ToTelemetryReasoningEffort(request.ReasoningEffort));
        using var telemetryActivity = activity;

        try
        {
            var transport = transportFactory.Create(runtime.WorkingDirectory);
            var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
            var session = new CodexRpcSession(transport, threadStore, sessionLogger);
            var result = await session.ExecuteTurnAsync(
                request.Prompt,
                runtime,
                telemetry.RequestId,
                telemetry.TraceId,
                onUpdate: null,
                timeoutCts.Token);

            return ToTurnResponse(result);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            logger.LogExceptionWithTrace(ex, telemetry.TraceId);
            throw new RuntimeTimeoutException("Codex App Server request timed out.", RuntimeName, runtime.TimeoutSeconds, telemetry.TraceId, ex);
        }
        catch (RuntimeFacadeException ex)
        {
            logger.LogError("Codex App Server request failed. Exception={Exception}", ex.ToString());
            throw;
        }
        catch (Exception ex) when (ex is not RuntimeFacadeException and not OperationCanceledException)
        {
            logger.LogExceptionWithTrace(ex, telemetry.TraceId);
            throw new RuntimeOperationException("Failed to execute Codex App Server request.", RuntimeName, telemetry.TraceId, null, null, ex);
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
        var (activity, telemetry) = AgentTelemetry.Start(RuntimeName, runtime.ModelId, ToTelemetryReasoningEffort(request.ReasoningEffort));
        using var telemetryActivity = activity;

        var channel = Channel.CreateUnbounded<CodexAppServerStreamingUpdate>();

        var sessionTask = Task.Run(async () =>
        {
            try
            {
                var transport = transportFactory.Create(runtime.WorkingDirectory);
                var sessionLogger = loggerFactory.CreateLogger<CodexRpcSession>();
                var session = new CodexRpcSession(transport, threadStore, sessionLogger);
                var result = await session.ExecuteTurnAsync(
                    request.Prompt,
                    runtime,
                    telemetry.RequestId,
                    telemetry.TraceId,
                    async update =>
                    {
                        await channel.Writer.WriteAsync(ToStreamingUpdate(update), timeoutCts.Token);
                    },
                    timeoutCts.Token);

                var finalKind = string.Equals(result.Status, "completed", StringComparison.Ordinal)
                    ? CodexAppServerStreamingUpdateKind.Completed
                    : CodexAppServerStreamingUpdateKind.Error;
                await channel.Writer.WriteAsync(
                    new CodexAppServerStreamingUpdate(
                        finalKind,
                        FinalText: result.Text,
                        ThreadId: result.ThreadId,
                        TurnId: result.TurnId,
                        Status: result.Status,
                        TraceId: result.TraceId,
                        RequestId: result.RequestId,
                        DiagnosticsSummary: result.DiagnosticsSummary,
                        ErrorSummary: result.ErrorSummary,
                        JsonRpcTurnStartRequestId: result.JsonRpcTurnStartRequestId),
                    timeoutCts.Token);

                if (finalKind == CodexAppServerStreamingUpdateKind.Error && result.ErrorSummary is not null)
                {
                    logger.LogWarning(
                        "Codex App Server turn completed with Status={Status}, ThreadId={ThreadId}, TurnId={TurnId}, ErrorSummary={ErrorSummary}",
                        result.Status,
                        result.ThreadId,
                        result.TurnId,
                        result.ErrorSummary);
                }

                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                logger.LogExceptionWithTrace(ex, telemetry.TraceId);
                channel.Writer.TryComplete(new RuntimeTimeoutException(
                    "Codex App Server streaming request timed out.",
                    RuntimeName,
                    runtime.TimeoutSeconds,
                    telemetry.TraceId,
                    ex));
            }
            catch (Exception ex)
            {
                if (ex is not RuntimeFacadeException and not OperationCanceledException)
                {
                    logger.LogExceptionWithTrace(ex, telemetry.TraceId);
                    channel.Writer.TryComplete(new RuntimeOperationException("Failed to execute Codex App Server streaming request.", RuntimeName, telemetry.TraceId, null, null, ex));
                    return;
                }

                logger.LogError("Codex App Server streaming request failed. Exception={Exception}", ex.ToString());
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
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

    private static CodexAppServerTurnResponse ToTurnResponse(CodexRpcTurnResult result)
        => new(
            result.Text,
            result.ThreadId,
            result.TurnId,
            result.Status,
            result.TraceId,
            result.RequestId,
            result.DiagnosticsSummary,
            result.ErrorSummary,
            result.JsonRpcTurnStartRequestId);

    private static CodexAppServerStreamingUpdate ToStreamingUpdate(CodexRpcStreamingUpdate update)
        => new(
            update.Kind,
            update.TextDelta,
            update.FinalText,
            update.ThreadId,
            update.TurnId,
            update.Status,
            update.TraceId,
            update.RequestId,
            update.DiagnosticsSummary,
            update.ErrorSummary,
            update.JsonRpcTurnStartRequestId);

    private static ReasoningEffortLevel? ToTelemetryReasoningEffort(CodexReasoningEffort? effort)
        => effort switch
        {
            CodexReasoningEffort.Low => ReasoningEffortLevel.Low,
            CodexReasoningEffort.Medium => ReasoningEffortLevel.Medium,
            CodexReasoningEffort.High => ReasoningEffortLevel.High,
            CodexReasoningEffort.XHigh => ReasoningEffortLevel.XHigh,
            _ => null,
        };

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
            request.CaptureEventsForDiagnostics ?? options.CaptureEventsForDiagnostics,
            NormalizeOutputSchema(request.OutputSchema));
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

    private static JsonElement? NormalizeOutputSchema(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (value.Value.ValueKind != JsonValueKind.Object)
        {
            throw new RuntimeInvalidRequestException("OutputSchema must be a JSON object.", RuntimeName);
        }

        return value.Value.Clone();
    }

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
