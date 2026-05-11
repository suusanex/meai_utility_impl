using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer;

internal sealed class CodexRpcSession(ICodexTransport transport, ILogger<CodexRpcSession> logger)
{
    private const string ProviderName = "CodexAppServer";
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, StringBuilder> _deltaByItemId = new(StringComparer.Ordinal);
    private readonly List<string> _deltaOrder = [];
    private readonly object _deltaOrderLock = new();
    private readonly TaskCompletionSource<TurnCompletion> _turnCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _nextRequestId;
    private string? _threadId;

    public async Task<string> ExecuteTurnAsync(
        string prompt,
        CodexRuntimeOptions runtimeOptions,
        Func<string, Task>? onDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        await transport.StartAsync(cancellationToken);
        var readLoopTask = RunReadLoopAsync(runtimeOptions, onDelta, cancellationToken);

        try
        {
            _ = await SendRequestAsync("initialize", CreateInitializeParams(runtimeOptions), cancellationToken);
            await SendNotificationAsync("initialized", null, cancellationToken);

            var threadStartResult = await SendRequestAsync("thread/start", CreateThreadStartParams(runtimeOptions), cancellationToken);
            _threadId = ExtractThreadId(threadStartResult);

            _ = await SendRequestAsync("turn/start", CreateTurnStartParams(_threadId, prompt, runtimeOptions), cancellationToken);

            using var cancelRegistration = cancellationToken.Register(() => _turnCompletion.TrySetCanceled(cancellationToken));
            var turnCompletion = await _turnCompletion.Task;

            return turnCompletion.Status switch
            {
                "completed" => turnCompletion.Text ?? string.Empty,
                "failed" => throw new ProviderException(turnCompletion.ErrorMessage ?? "Codex turn failed.", ProviderName),
                "interrupted" => throw new OperationCanceledException("Codex turn was interrupted.", cancellationToken),
                _ => throw new ProviderException($"Unknown codex turn status '{turnCompletion.Status}'.", ProviderName),
            };
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
        Func<string, Task>? onDelta,
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
                    await HandleNotificationAsync(root, methodElement.GetString(), onDelta, cancellationToken);
                }
            }

            var eofException = CreateProcessExitedException();
            FailPending(eofException);
            _turnCompletion.TrySetException(eofException);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
            tcs.TrySetException(new ProviderException(message, ProviderName));
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
        Func<string, Task>? onDelta,
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
                var delta = GetRequiredString(parameters, "delta");
                _ = GetRequiredString(parameters, "threadId");
                _ = GetRequiredString(parameters, "turnId");

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

                if (onDelta is not null && !string.IsNullOrEmpty(delta))
                {
                    await onDelta(delta);
                }

                break;
            }
            case "turn/completed":
            {
                var parameters = GetRequiredProperty(root, "params");
                _ = GetRequiredString(parameters, "threadId");
                var turn = GetRequiredProperty(parameters, "turn");
                _ = GetRequiredString(turn, "id");
                var status = GetRequiredString(turn, "status");
                var text = BuildAggregatedText(turn);
                var errorMessage = GetOptionalNestedString(turn, "error", "message");

                _turnCompletion.TrySetResult(new TurnCompletion(status, text, errorMessage));
                break;
            }
            case "error":
            {
                var parameters = GetRequiredProperty(root, "params");
                _ = GetRequiredString(parameters, "threadId");
                _ = GetRequiredString(parameters, "turnId");
                var willRetry = GetRequiredBoolean(parameters, "willRetry");
                if (!willRetry)
                {
                    var error = GetRequiredProperty(parameters, "error");
                    var errorMessage = GetRequiredString(error, "message");
                    _turnCompletion.TrySetException(new ProviderException(errorMessage, ProviderName));
                }

                break;
            }
            case "thread/status/changed":
            {
                var parameters = GetRequiredProperty(root, "params");
                _ = GetRequiredString(parameters, "threadId");
                var status = GetRequiredProperty(parameters, "status");
                var statusType = GetRequiredString(status, "type");
                if (!string.Equals(statusType, "active", StringComparison.Ordinal))
                {
                    break;
                }

                var flags = GetRequiredProperty(status, "activeFlags");
                if (flags.ValueKind != JsonValueKind.Array)
                {
                    throw new ProviderException("Property 'activeFlags' must be an array when status.type is 'active'.", ProviderName);
                }

                foreach (var flag in flags.EnumerateArray())
                {
                    var value = flag.GetString();
                    if (string.Equals(value, "waitingOnUserInput", StringComparison.Ordinal))
                    {
                        _turnCompletion.TrySetException(new ProviderException("User input required by codex app-server.", ProviderName));
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
            JsonValueKind.String => idElement.GetString() ?? throw new ProviderException("Server request id is empty.", ProviderName),
            _ => throw new ProviderException("Server request id must be string or number.", ProviderName),
        };

        if (method is "item/commandExecution/requestApproval"
            or "item/fileChange/requestApproval"
            or "item/permissions/requestApproval")
        {
            var decision = runtimeOptions.AutoApprove ? "acceptForSession" : "cancel";
            await SendServerResponseAsync(id, new Dictionary<string, object?> { ["decision"] = decision }, cancellationToken);
            if (!runtimeOptions.AutoApprove)
            {
                throw new ProviderException($"Approval requested: {method}", ProviderName);
            }

            return;
        }

        if (method is "item/tool/requestUserInput" or "mcpServer/elicitation/request")
        {
            await SendServerResponseAsync(id, new Dictionary<string, object?> { ["decision"] = "cancel" }, cancellationToken);
            throw new ProviderException($"User interaction is not supported: {method}", ProviderName);
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

    private async Task<JsonElement?> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
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
        return await tcs.Task;
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
                ["message"] = "Method not supported by MeAiUtility.MultiProvider CodexAppServer provider.",
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
        _ => throw new InvalidRequestException(
            "SandboxMode must be one of: read-only, workspace-write, danger-full-access.",
            ProviderName),
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
            throw new ProviderException("thread/start response was empty.", ProviderName);
        }

        var root = threadStartResult.Value;
        if (!root.TryGetProperty("thread", out var thread)
            || !thread.TryGetProperty("id", out var threadIdElement)
            || threadIdElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(threadIdElement.GetString()))
        {
            throw new ProviderException("thread/start response did not contain thread.id.", ProviderName);
        }

        return threadIdElement.GetString()!;
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
        JsonValueKind.String => idElement.GetString() ?? throw new ProviderException("JSON-RPC id cannot be null.", ProviderName),
        _ => throw new ProviderException("JSON-RPC id must be string or number.", ProviderName),
    };

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new ProviderException($"Required property '{propertyName}' was not found in codex message.", ProviderName);
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new ProviderException($"Property '{propertyName}' must be a non-empty string.", ProviderName);
        }

        return property.GetString()!;
    }

    private static bool GetRequiredBoolean(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new ProviderException($"Property '{propertyName}' must be a boolean.", ProviderName);
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

    private sealed record TurnCompletion(string Status, string? Text, string? ErrorMessage);
}
