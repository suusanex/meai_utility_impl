using System.Runtime.CompilerServices;
using MultiCodingAgentFacade.Core.Diagnostics;
using MultiCodingAgentFacade.Core.Exceptions;
using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MultiCodingAgentFacade.GitHubCopilot;

public sealed class GitHubCopilotAgentClient(
    ICopilotSdkWrapper sdkWrapper,
    GitHubCopilotOptions options,
    ILogger<GitHubCopilotAgentClient>? logger = null)
{
    private const string RuntimeName = GitHubCopilotRuntimeMarker.RuntimeName;
    private readonly ILogger<GitHubCopilotAgentClient> logger = logger ?? NullLogger<GitHubCopilotAgentClient>.Instance;

    public async Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await sdkWrapper.ListModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not RuntimeFacadeException and not OperationCanceledException)
        {
            var traceId = Guid.NewGuid().ToString("N");
            this.logger.LogExceptionWithTrace(ex, traceId);
            throw new RuntimeOperationException("Failed to list GitHub Copilot models.", RuntimeName, traceId, null, null, ex);
        }
    }

    public async Task<GitHubCopilotAgentResponse> SendTurnAsync(GitHubCopilotAgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var modelId = request.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = request.ReasoningEffort ?? options.ReasoningEffort;
        var (activity, telemetry) = AgentTelemetry.Start(RuntimeName, modelId, reasoning);
        using var scope = CreateRequestScope(telemetry);

        try
        {
            ValidateRequest(request);
            await ValidateModelAsync(modelId, reasoning, cancellationToken).ConfigureAwait(false);
            var config = BuildSessionConfig(request, modelId, reasoning, telemetry);
            LogModelProvider(config.ModelProvider);
            var startedAt = DateTimeOffset.UtcNow;
            var sdkResponse = await sdkWrapper.SendAsync(request.Prompt, config, cancellationToken).ConfigureAwait(false);
            return new GitHubCopilotAgentResponse(
                sdkResponse.Text,
                modelId,
                telemetry.TraceId,
                telemetry.RequestId,
                RuntimeName,
                DateTimeOffset.UtcNow - startedAt,
                sdkResponse.FinishStatus,
                sdkResponse.DiagnosticsSummary,
                sdkResponse.SdkMetadata);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            this.logger.LogInformation("GitHub Copilot request cancelled. Stage=cancellation; RequestId={RequestId}; Exception={Exception}", telemetry.RequestId, ex.ToString());
            throw;
        }
        catch (TimeoutException ex)
        {
            this.logger.LogExceptionWithTrace(ex, telemetry.TraceId);
            throw new RuntimeTimeoutException("GitHub Copilot request timed out.", RuntimeName, request.TimeoutSeconds ?? options.TimeoutSeconds, telemetry.TraceId, ex);
        }
        catch (Exception ex) when (ex is not RuntimeFacadeException and not OperationCanceledException)
        {
            this.logger.LogExceptionWithTrace(ex, telemetry.TraceId);
            throw new RuntimeOperationException("Failed to execute GitHub Copilot request.", RuntimeName, telemetry.TraceId, null, null, ex);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public async IAsyncEnumerable<GitHubCopilotStreamingUpdate> StreamTurnAsync(
        GitHubCopilotAgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!sdkWrapper.SupportsStreaming)
        {
            throw new RuntimeFeatureNotSupportedException("Streaming is not supported by the configured GitHub Copilot SDK wrapper.", RuntimeName, "Streaming");
        }

        var modelId = request.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = request.ReasoningEffort ?? options.ReasoningEffort;
        var (activity, telemetry) = AgentTelemetry.Start(RuntimeName, modelId, reasoning);
        using var scope = CreateRequestScope(telemetry);

        try
        {
            ValidateRequest(request);
            await ValidateModelAsync(modelId, reasoning, cancellationToken).ConfigureAwait(false);
            var config = BuildSessionConfig(request, modelId, reasoning, telemetry);
            config.Streaming = true;
            LogModelProvider(config.ModelProvider);
            var startedAt = DateTimeOffset.UtcNow;

            await foreach (var update in sdkWrapper.SendStreamingAsync(request.Prompt, config, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var elapsed = DateTimeOffset.UtcNow - startedAt;
                yield return update.Kind switch
                {
                    CopilotStreamingUpdateKind.Delta => new GitHubCopilotStreamingUpdate(
                        GitHubCopilotStreamingUpdateKind.Delta,
                        update.TextDelta,
                        DeltaCount: update.DeltaCount,
                        AccumulatedLength: update.AccumulatedLength,
                        TraceId: telemetry.TraceId,
                        RequestId: telemetry.RequestId,
                        RuntimeName: RuntimeName,
                        ElapsedTime: elapsed,
                        SdkMetadata: update.SdkMetadata),
                    CopilotStreamingUpdateKind.Progress => new GitHubCopilotStreamingUpdate(
                        GitHubCopilotStreamingUpdateKind.Progress,
                        DeltaCount: update.DeltaCount,
                        AccumulatedLength: update.AccumulatedLength,
                        TraceId: telemetry.TraceId,
                        RequestId: telemetry.RequestId,
                        RuntimeName: RuntimeName,
                        ElapsedTime: elapsed,
                        SdkMetadata: update.SdkMetadata),
                    CopilotStreamingUpdateKind.Completed => new GitHubCopilotStreamingUpdate(
                        GitHubCopilotStreamingUpdateKind.Completed,
                        FinalText: update.FinalText,
                        DeltaCount: update.DeltaCount,
                        AccumulatedLength: update.AccumulatedLength,
                        TraceId: telemetry.TraceId,
                        RequestId: telemetry.RequestId,
                        RuntimeName: RuntimeName,
                        ElapsedTime: elapsed,
                        FinishStatus: update.FinishStatus,
                        DiagnosticsSummary: update.DiagnosticsSummary,
                        SdkMetadata: update.SdkMetadata),
                    _ => throw new RuntimeOperationException($"Unsupported GitHub Copilot streaming update '{update.Kind}'.", RuntimeName, telemetry.TraceId),
                };
            }
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private async Task ValidateModelAsync(string modelId, ReasoningEffortLevel? reasoning, CancellationToken cancellationToken)
    {
        var models = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
        var selected = models.FirstOrDefault(model => string.Equals(model.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            throw new RuntimeInvalidRequestException($"Unknown GitHub Copilot model id '{modelId}'. Valid model ids: {string.Join(", ", models.Select(model => model.ModelId))}", RuntimeName);
        }

        var requestedReasoningEffort = MapReasoningEffort(reasoning);
        if (requestedReasoningEffort is not null && !selected.SupportedReasoningEfforts.Contains(requestedReasoningEffort, StringComparer.OrdinalIgnoreCase))
        {
            var supportedValues = selected.SupportedReasoningEfforts.Count == 0
                ? "(none)"
                : string.Join(", ", selected.SupportedReasoningEfforts);
            throw new RuntimeFeatureNotSupportedException(
                $"Reasoning effort '{reasoning}' is not supported by selected model. Supported values: {supportedValues}.",
                RuntimeName,
                "ReasoningEffort");
        }
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
            _ => throw new RuntimeInvalidRequestException($"Unsupported reasoning effort '{reasoningEffort}'.", RuntimeName),
        };
    }

    private static void ValidateRequest(GitHubCopilotAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new RuntimeInvalidRequestException("Prompt must be specified.", RuntimeName);
        }

        if (request.TimeoutSeconds is <= 0)
        {
            throw new RuntimeInvalidRequestException("TimeoutSeconds must be greater than zero.", RuntimeName);
        }

        if (request.Attachments is null)
        {
            return;
        }

        foreach (var attachment in request.Attachments)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Path))
            {
                throw new RuntimeInvalidRequestException("Attachment path must be specified.", RuntimeName);
            }

            if (!Path.IsPathRooted(attachment.Path))
            {
                throw new RuntimeInvalidRequestException("Attachment path must be an absolute path.", RuntimeName);
            }
        }
    }

    private CopilotSessionConfig BuildSessionConfig(GitHubCopilotAgentRequest request, string modelId, ReasoningEffortLevel? reasoning, AgentTelemetry telemetry)
    {
        var config = new CopilotSessionConfig
        {
            ModelId = modelId,
            ReasoningEffort = reasoning,
            Streaming = request.Streaming ?? options.Streaming,
            Attachments = request.Attachments,
            SkillDirectories = request.SkillDirectories,
            DisabledSkills = request.DisabledSkills,
            TimeoutSeconds = request.TimeoutSeconds,
            ModelProvider = request.ModelProvider ?? options.ModelProvider,
            InfiniteSessions = request.InfiniteSessions ?? options.InfiniteSessions,
            PermissionHandling = request.PermissionHandling ?? options.PermissionHandling,
            TraceId = telemetry.TraceId,
            RequestId = telemetry.RequestId,
        };

        CopyAdvancedOptions(request, config.AdvancedOptions);
        return config;
    }

    private static void CopyAdvancedOptions(GitHubCopilotAgentRequest request, Dictionary<string, object?> target)
    {
        if (request.AdvancedOptions is not null)
        {
            foreach (var item in request.AdvancedOptions)
            {
                target[item.Key] = item.Value;
            }
        }

        AddIfNotNull(target, "copilot.configDir", request.ConfigDir);
        AddIfNotNull(target, "copilot.workingDirectory", request.WorkingDirectory);
        AddIfNotNull(target, "copilot.availableTools", request.AvailableTools);
        AddIfNotNull(target, "copilot.excludedTools", request.ExcludedTools);
        AddIfNotNull(target, "copilot.mcpServers", request.McpServers);
        AddIfNotNull(target, "copilot.agent", request.Agent);
        AddIfNotNull(target, "copilot.mode", request.Mode);
    }

    private static void AddIfNotNull(Dictionary<string, object?> target, string key, object? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }

    private IDisposable? CreateRequestScope(AgentTelemetry telemetry)
        => logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = telemetry.TraceId,
            ["RequestId"] = telemetry.RequestId,
            ["Runtime"] = RuntimeName,
        });

    private void LogModelProvider(GitHubCopilotModelProviderOptions? modelProvider)
    {
        if (modelProvider is null)
        {
            return;
        }

        logger.LogDebug(
            "GitHub Copilot SDK model provider applied. Type={Type}; BaseUrl={BaseUrl}; AzureApiVersion={AzureApiVersion}; HasApiKey={HasApiKey}; HasBearerToken={HasBearerToken}",
            modelProvider.Type,
            modelProvider.BaseUrl,
            modelProvider.AzureApiVersion,
            !string.IsNullOrWhiteSpace(modelProvider.ApiKey),
            !string.IsNullOrWhiteSpace(modelProvider.BearerToken));
    }
}
