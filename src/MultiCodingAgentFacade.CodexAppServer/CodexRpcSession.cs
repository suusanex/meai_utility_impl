using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Threading;
using MultiCodingAgentFacade.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace MultiCodingAgentFacade.CodexAppServer;

internal sealed class CodexRpcSession(ICodexTransport transport, ICodexThreadStore threadStore, ILogger<CodexRpcSession> logger)
{
    private const string RuntimeName = CodexAppServerRuntimeMarker.RuntimeName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, StringBuilder> _deltaByItemId = new(StringComparer.Ordinal);
    private readonly List<string> _deltaOrder = [];
    private readonly object _deltaOrderLock = new();
    private readonly TaskCompletionSource<TurnCompletion> _turnCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _nextRequestId;

    public async Task<CodexRpcTurnResult> ExecuteTurnAsync(
        string prompt,
        CodexRuntimeOptions runtimeOptions,
        string? requestId,
        string? traceId,
        Func<CodexRpcStreamingUpdate, Task>? onUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        await transport.StartAsync(cancellationToken);
        var readLoopTask = RunReadLoopAsync(runtimeOptions, requestId, traceId, onUpdate, cancellationToken);

        try
        {
            _ = await SendRequestAsync("initialize", CreateInitializeParams(runtimeOptions), cancellationToken);
            await SendNotificationAsync("initialized", null, cancellationToken);

            var resolution = await ResolveThreadAsync(runtimeOptions, cancellationToken);
            var turnStartResponse = await SendRequestAsync("turn/start", CreateTurnStartParams(resolution.ThreadId, prompt, runtimeOptions), cancellationToken);
            var turnStartTurnId = ExtractOptionalTurnId(turnStartResponse.Result);
            if (resolution.RecordToTouch is not null)
            {
                await threadStore.SaveAsync(
                    resolution.RecordToTouch with { LastUsedAt = DateTimeOffset.UtcNow },
                    runtimeOptions.ThreadStorePath,
                    cancellationToken);
            }

            using var cancelRegistration = cancellationToken.Register(() => _turnCompletion.TrySetCanceled(cancellationToken));
            var turnCompletion = await _turnCompletion.Task;
            var status = NormalizeStatus(turnCompletion.Status);

            return new CodexRpcTurnResult(
                status == "completed" ? turnCompletion.Text ?? string.Empty : string.Empty,
                turnCompletion.ThreadId ?? resolution.ThreadId,
                turnCompletion.TurnId ?? turnStartTurnId,
                status,
                traceId,
                requestId,
                BuildDiagnosticsSummary(),
                turnCompletion.ErrorSummary,
                turnStartResponse.RequestId);
        }
        finally
        {
            await transport.DisposeAsync();
            try
            {
                await readLoopTask;
            }
            catch (Exception ex) when (_turnCompletion.Task.IsCompleted)
            {
                logger.LogDebug("Read loop completed after turn completion. Exception={Exception}", ex.ToString());
            }
        }
    }

    private async Task RunReadLoopAsync(
        CodexRuntimeOptions runtimeOptions,
        string? requestId,
        string? traceId,
        Func<CodexRpcStreamingUpdate, Task>? onUpdate,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in transport.ReadLinesAsync(cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (runtimeOptions.CaptureEventsForDiagnostics)
                {
                    logger.LogDebug("codex event: {Line}", line);
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.TryGetProperty("id", out var idElement))
                {
                    if (root.TryGetProperty("method", out _))
                    {
                        await HandleServerRequestAsync(root, runtimeOptions, cancellationToken);
                    }
                    else
                    {
                        HandleResponse(root, idElement);
                    }

                    continue;
                }

                if (root.TryGetProperty("method", out var methodElement))
                {
                    await HandleNotificationAsync(root, methodElement.GetString(), requestId, traceId, onUpdate, cancellationToken);
                }
            }

            var eofException = CreateProcessExitedException();
            FailPending(eofException);
            _turnCompletion.TrySetException(eofException);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Codex read loop was canceled. Exception={Exception}", ex.ToString());
            _turnCompletion.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError("Codex read loop failed. Exception={Exception}", ex.ToString());
            FailPending(ex);
            _turnCompletion.TrySetException(ex);
        }
    }

    private void HandleResponse(JsonElement root, JsonElement idElement)
    {
        var requestId = JsonElementToIdKey(idElement);
        if (!_pending.TryRemove(requestId, out var tcs))
        {
            return;
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            var message = ExtractErrorMessage(errorElement);
            tcs.TrySetException(new RuntimeOperationException(message, RuntimeName));
            return;
        }

        if (root.TryGetProperty("result", out var resultElement))
        {
            tcs.TrySetResult(resultElement.Clone());
            return;
        }

        tcs.TrySetResult(null);
    }

    private async Task HandleNotificationAsync(
        JsonElement root,
        string? method,
        string? requestId,
        string? traceId,
        Func<CodexRpcStreamingUpdate, Task>? onUpdate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        switch (method)
        {
            case "item/agentMessage/delta":
            {
                var parameters = GetRequiredProperty(root, "params");
                var itemId = GetRequiredString(parameters, "itemId");
                var delta = GetRequiredStringOrEmpty(parameters, "delta");
                var threadId = GetRequiredString(parameters, "threadId");
                var turnId = GetRequiredString(parameters, "turnId");

                if (string.IsNullOrEmpty(delta))
                {
                    break;
                }

                if (_deltaByItemId.TryAdd(itemId, new StringBuilder(delta)))
                {
                    lock (_deltaOrderLock)
                    {
                        _deltaOrder.Add(itemId);
                    }
                }
                else
                {
                    _deltaByItemId[itemId].Append(delta);
                }

                if (onUpdate is not null && !string.IsNullOrEmpty(delta))
                {
                    await onUpdate(
                        new CodexRpcStreamingUpdate(
                            CodexAppServerStreamingUpdateKind.Delta,
                            delta,
                            null,
                            threadId,
                            turnId,
                            null,
                            traceId,
                            requestId,
                            BuildDiagnosticsSummary(),
                            null,
                            null));
                }

                break;
            }
            case "turn/completed":
            {
                var parameters = GetRequiredProperty(root, "params");
                var threadId = GetRequiredString(parameters, "threadId");
                var turn = GetRequiredProperty(parameters, "turn");
                var turnId = GetRequiredString(turn, "id");
                var status = GetRequiredString(turn, "status");
                var text = BuildAggregatedText(turn);
                var errorMessage = GetOptionalNestedString(turn, "error", "message");

                _turnCompletion.TrySetResult(new TurnCompletion(threadId, turnId, status, text, errorMessage));
                break;
            }
            case "error":
            {
                var parameters = GetRequiredProperty(root, "params");
                var threadId = GetRequiredString(parameters, "threadId");
                var turnId = GetRequiredString(parameters, "turnId");
                var willRetry = GetRequiredBoolean(parameters, "willRetry");
                var error = GetRequiredProperty(parameters, "error");
                var errorMessage = GetRequiredString(error, "message");
                if (!willRetry)
                {
                    _turnCompletion.TrySetResult(new TurnCompletion(threadId, turnId, "error", string.Empty, errorMessage));
                }
                else if (onUpdate is not null)
                {
                    await onUpdate(
                        new CodexRpcStreamingUpdate(
                            CodexAppServerStreamingUpdateKind.StatusChanged,
                            null,
                            null,
                            threadId,
                            turnId,
                            "retrying",
                            traceId,
                            requestId,
                            BuildDiagnosticsSummary(),
                            errorMessage,
                            null));
                }

                break;
            }
            case "thread/status/changed":
            {
                var parameters = GetRequiredProperty(root, "params");
                var threadId = GetRequiredString(parameters, "threadId");
                var status = GetRequiredProperty(parameters, "status");
                var statusType = GetRequiredString(status, "type");
                if (onUpdate is not null)
                {
                    await onUpdate(
                        new CodexRpcStreamingUpdate(
                            CodexAppServerStreamingUpdateKind.StatusChanged,
                            null,
                            null,
                            threadId,
                            null,
                            statusType,
                            traceId,
                            requestId,
                            BuildDiagnosticsSummary(),
                            null,
                            null));
                }

                if (!string.Equals(statusType, "active", StringComparison.Ordinal))
                {
                    break;
                }

                var flags = GetRequiredProperty(status, "activeFlags");
                if (flags.ValueKind != JsonValueKind.Array)
                {
                    throw new RuntimeOperationException("Property 'activeFlags' must be an array when status.type is 'active'.", RuntimeName);
                }

                foreach (var flag in flags.EnumerateArray())
                {
                    var value = flag.GetString();
                    if (string.Equals(value, "waitingOnUserInput", StringComparison.Ordinal))
                    {
                        _turnCompletion.TrySetResult(
                            new TurnCompletion(
                                threadId,
                                null,
                                "waitingOnUserInput",
                                string.Empty,
                                "User input required by codex app-server."));
                    }
                }

                break;
            }
        }
    }

    private async Task HandleServerRequestAsync(JsonElement root, CodexRuntimeOptions runtimeOptions, CancellationToken cancellationToken)
    {
        var method = GetRequiredString(root, "method");
        var idElement = GetRequiredProperty(root, "id");
        object id = idElement.ValueKind switch
        {
            JsonValueKind.Number => idElement.GetInt64(),
            JsonValueKind.String => idElement.GetString() ?? throw new RuntimeOperationException("Server request id is empty.", RuntimeName),
            _ => throw new RuntimeOperationException("Server request id must be string or number.", RuntimeName),
        };

        if (method is "item/commandExecution/requestApproval"
            or "item/fileChange/requestApproval"
            or "item/permissions/requestApproval")
        {
            var decision = runtimeOptions.AutoApprove ? "acceptForSession" : "cancel";
            await SendServerResponseAsync(id, new Dictionary<string, object?> { ["decision"] = decision }, cancellationToken);
            if (!runtimeOptions.AutoApprove)
            {
                throw new RuntimeOperationException($"Approval requested: {method}", RuntimeName);
            }

            return;
        }

        if (method is "item/tool/requestUserInput" or "mcpServer/elicitation/request")
        {
            await SendServerResponseAsync(id, new Dictionary<string, object?> { ["decision"] = "cancel" }, cancellationToken);
            throw new RuntimeOperationException($"User interaction is not supported: {method}", RuntimeName);
        }

        await SendServerErrorAsync(id, cancellationToken);
    }

    private CodexProcessExitedException CreateProcessExitedException()
    {
        if (transport is not ICodexTransportDiagnostics diagnostics)
        {
            return new CodexProcessExitedException();
        }

        return new CodexProcessExitedException(
            diagnostics.CommandForDiagnostics,
            diagnostics.ArgumentsForDiagnostics,
            diagnostics.ExitCodeForDiagnostics,
            diagnostics.StderrTailForDiagnostics);
    }

    private async Task<JsonRpcResponse> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, tcs))
        {
            throw new InvalidOperationException($"Duplicate pending request id '{requestId}'.");
        }

        var envelope = new Dictionary<string, object?>
        {
            ["id"] = long.Parse(requestId, System.Globalization.CultureInfo.InvariantCulture),
            ["method"] = method,
        };

        if (parameters is not null)
        {
            envelope["params"] = parameters;
        }

        await transport.SendLineAsync(JsonSerializer.Serialize(envelope), cancellationToken);

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        var result = await tcs.Task;
        return new JsonRpcResponse(requestId, result);
    }

    private Task SendNotificationAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["method"] = method,
        };

        if (parameters is not null)
        {
            envelope["params"] = parameters;
        }

        return transport.SendLineAsync(JsonSerializer.Serialize(envelope), cancellationToken);
    }

    private Task SendServerResponseAsync(object id, object result, CancellationToken cancellationToken)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["result"] = result,
        };

        return transport.SendLineAsync(JsonSerializer.Serialize(envelope), cancellationToken);
    }

    private Task SendServerErrorAsync(object id, CancellationToken cancellationToken)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = -32601,
                ["message"] = "Method not supported by MultiCodingAgentFacade CodexAppServer provider.",
            },
        };

        return transport.SendLineAsync(JsonSerializer.Serialize(envelope), cancellationToken);
    }

    private static object CreateInitializeParams(CodexRuntimeOptions runtimeOptions)
    {
        return new Dictionary<string, object?>
        {
            ["clientInfo"] = new Dictionary<string, object?>
            {
                ["name"] = runtimeOptions.ClientName,
                ["version"] = runtimeOptions.ClientVersion,
            },
        };
    }

    private static object CreateThreadStartParams(CodexRuntimeOptions runtimeOptions)
    {
        var parameters = new Dictionary<string, object?>();
        AddIfNotNull(parameters, "model", runtimeOptions.ModelId);
        AddIfNotNull(parameters, "cwd", runtimeOptions.WorkingDirectory);
        AddIfNotNull(parameters, "approvalPolicy", runtimeOptions.ApprovalPolicy);
        AddIfNotNull(parameters, "sandbox", runtimeOptions.SandboxMode);
        AddIfNotNull(parameters, "serviceName", runtimeOptions.ServiceName);
        AddIfNotNull(parameters, "personality", runtimeOptions.Personality);
        return parameters;
    }

    private static object CreateTurnStartParams(string threadId, string prompt, CodexRuntimeOptions runtimeOptions)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["threadId"] = threadId,
            ["input"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["text"] = prompt,
                },
            },
        };

        AddIfNotNull(parameters, "model", runtimeOptions.ModelId);
        AddIfNotNull(parameters, "effort", runtimeOptions.ReasoningEffort);
        AddIfNotNull(parameters, "cwd", runtimeOptions.WorkingDirectory);
        AddIfNotNull(parameters, "approvalPolicy", runtimeOptions.ApprovalPolicy);
        AddIfNotNull(parameters, "sandboxPolicy", CreateSandboxPolicy(runtimeOptions));
        AddIfNotNull(parameters, "summary", runtimeOptions.Summary);
        AddIfNotNull(parameters, "personality", runtimeOptions.Personality);
        return parameters;
    }

    private static object CreateSandboxPolicy(CodexRuntimeOptions runtimeOptions) => runtimeOptions.SandboxMode switch
    {
        "workspace-write" => new Dictionary<string, object?>
        {
            ["type"] = "workspaceWrite",
            ["networkAccess"] = runtimeOptions.NetworkAccess,
        },
        "read-only" => new Dictionary<string, object?>
        {
            ["type"] = "readOnly",
            ["networkAccess"] = runtimeOptions.NetworkAccess,
        },
        "danger-full-access" => new Dictionary<string, object?>
        {
            ["type"] = "dangerFullAccess",
        },
        _ => throw new RuntimeInvalidRequestException(
            "SandboxMode must be one of: read-only, workspace-write, danger-full-access.",
            RuntimeName),
    };

    private string BuildAggregatedText(JsonElement turn)
    {
        var status = GetRequiredString(turn, "status");
        if (status == "failed")
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        lock (_deltaOrderLock)
        {
            foreach (var itemId in _deltaOrder)
            {
                if (_deltaByItemId.TryGetValue(itemId, out var deltaBuilder))
                {
                    builder.Append(deltaBuilder);
                }
            }
        }

        if (builder.Length > 0)
        {
            return builder.ToString();
        }

        if (!turn.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeElement)
                || !string.Equals(typeElement.GetString(), "agentMessage", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
            {
                builder.Append(textElement.GetString());
            }
        }

        return builder.ToString();
    }

    private static string ExtractThreadId(JsonElement? threadStartResult)
    {
        if (threadStartResult is null)
        {
            throw new RuntimeOperationException("thread/start response was empty.", RuntimeName);
        }

        var root = threadStartResult.Value;
        if (!root.TryGetProperty("thread", out var thread)
            || !thread.TryGetProperty("id", out var threadIdElement)
            || threadIdElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(threadIdElement.GetString()))
        {
            throw new RuntimeOperationException("thread/start response did not contain thread.id.", RuntimeName);
        }

        return threadIdElement.GetString()!;
    }

    private static string? ExtractOptionalTurnId(JsonElement? turnStartResult)
    {
        if (turnStartResult is null)
        {
            return null;
        }

        var root = turnStartResult.Value;
        if (!root.TryGetProperty("turn", out var turn)
            || !turn.TryGetProperty("id", out var turnIdElement)
            || turnIdElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(turnIdElement.GetString()))
        {
            return null;
        }

        return turnIdElement.GetString();
    }

    private static string NormalizeStatus(string status)
        => string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim();

    private string? BuildDiagnosticsSummary()
    {
        if (transport is not ICodexTransportDiagnostics diagnostics)
        {
            return null;
        }

        var values = new List<string>();
        AddDiagnostic(values, "Command", diagnostics.CommandForDiagnostics);
        if (diagnostics.ExitCodeForDiagnostics is not null)
        {
            values.Add($"ExitCode={diagnostics.ExitCodeForDiagnostics.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        AddDiagnostic(values, "StderrTail", diagnostics.StderrTailForDiagnostics);
        return values.Count == 0 ? null : string.Join("; ", values);
    }

    private static void AddDiagnostic(ICollection<string> values, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        values.Add($"{name}='{value}'");
    }

    private static string ExtractErrorMessage(JsonElement errorElement)
    {
        if (errorElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
        {
            return messageElement.GetString()!;
        }

        return "Codex JSON-RPC error response.";
    }

    private static string JsonElementToIdKey(JsonElement idElement) => idElement.ValueKind switch
    {
        JsonValueKind.Number => idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
        JsonValueKind.String => idElement.GetString() ?? throw new RuntimeOperationException("JSON-RPC id cannot be null.", RuntimeName),
        _ => throw new RuntimeOperationException("JSON-RPC id must be string or number.", RuntimeName),
    };

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new RuntimeOperationException($"Required property '{propertyName}' was not found in codex message.", RuntimeName);
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new RuntimeOperationException($"Property '{propertyName}' must be a non-empty string.", RuntimeName);
        }

        return property.GetString()!;
    }

    private static bool GetRequiredBoolean(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new RuntimeOperationException($"Property '{propertyName}' must be a boolean.", RuntimeName);
        }

        return property.GetBoolean();
    }

    private static string? GetOptionalNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nested) || nested.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!nested.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string GetRequiredStringOrEmpty(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            throw new RuntimeOperationException($"Property '{propertyName}' must be a string.", RuntimeName);
        }

        return property.GetString() ?? string.Empty;
    }

    private static void AddIfNotNull(IDictionary<string, object?> dictionary, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        dictionary[key] = value;
    }

    private void FailPending(Exception exception)
    {
        foreach (var pending in _pending.ToArray())
        {
            if (_pending.TryRemove(pending.Key, out var tcs))
            {
                tcs.TrySetException(exception);
            }
        }
    }

    private sealed record TurnCompletion(string? ThreadId, string? TurnId, string Status, string? Text, string? ErrorSummary);

    private async Task<ThreadResolution> ResolveThreadAsync(CodexRuntimeOptions runtimeOptions, CancellationToken cancellationToken)
    {
        switch (runtimeOptions.ThreadReusePolicy)
        {
            case CodexThreadReusePolicy.AlwaysNew:
                return new ThreadResolution(await StartNewThreadAsync(runtimeOptions, cancellationToken), null);

            case CodexThreadReusePolicy.ReuseByThreadId:
                if (runtimeOptions.ThreadId is null)
                {
                    throw new RuntimeInvalidRequestException("ThreadId must be configured when ThreadReusePolicy is ReuseByThreadId.", RuntimeName);
                }

                return new ThreadResolution(runtimeOptions.ThreadId, null, null);

            case CodexThreadReusePolicy.ReuseOrCreateByKey:
                if (runtimeOptions.ThreadKey is null)
                {
                    throw new RuntimeInvalidRequestException("ThreadKey must be configured when ThreadReusePolicy is ReuseOrCreateByKey.", RuntimeName);
                }

                var record = await threadStore.TryGetByKeyAsync(runtimeOptions.ThreadKey, runtimeOptions.ThreadStorePath, cancellationToken);
                if (record is not null)
                {
                    return new ThreadResolution(record.ThreadId, record, null);
                }

                var threadStart = await StartNewThreadAsync(runtimeOptions, cancellationToken);
                var now = DateTimeOffset.UtcNow;
                await threadStore.SaveAsync(
                    new CodexThreadRecord(
                        runtimeOptions.ThreadKey,
                        threadStart.ThreadId,
                        runtimeOptions.ThreadName,
                        runtimeOptions.WorkingDirectory,
                        runtimeOptions.ModelId,
                        now,
                        now),
                    runtimeOptions.ThreadStorePath,
                    cancellationToken);

                return new ThreadResolution(threadStart, null);

            default:
                throw new RuntimeInvalidRequestException($"Unsupported ThreadReusePolicy '{runtimeOptions.ThreadReusePolicy}'.", RuntimeName);
        }
    }

    private async Task<ThreadStartResolution> StartNewThreadAsync(CodexRuntimeOptions runtimeOptions, CancellationToken cancellationToken)
    {
        var threadStartResult = await SendRequestAsync("thread/start", CreateThreadStartParams(runtimeOptions), cancellationToken);
        return new ThreadStartResolution(ExtractThreadId(threadStartResult.Result), threadStartResult.RequestId);
    }

    private sealed record JsonRpcResponse(string RequestId, JsonElement? Result);

    private sealed record ThreadStartResolution(string ThreadId, string JsonRpcRequestId);

    private sealed record ThreadResolution(string ThreadId, CodexThreadRecord? RecordToTouch, string? JsonRpcThreadStartRequestId)
    {
        public ThreadResolution(ThreadStartResolution startResolution, CodexThreadRecord? recordToTouch)
            : this(startResolution.ThreadId, recordToTouch, startResolution.JsonRpcRequestId)
        {
        }
    }
}
