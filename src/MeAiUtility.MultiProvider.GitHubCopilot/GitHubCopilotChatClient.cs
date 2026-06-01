using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotChatClient(CopilotClientHost host, GitHubCopilotProviderOptions options, ILogger<GitHubCopilotChatClient> logger) : IChatClient, IProviderCapabilities, ICopilotModelCatalog
{
    public bool SupportsReasoningEffort => true;
    public bool SupportsStreaming => host.Wrapper.SupportsStreaming;
    public bool SupportsModelDiscovery => true;
    public bool SupportsEmbeddings => false;
    public bool SupportsProviderOverride => true;
    public bool SupportsExtensionParameters => true;

    public bool IsSupported(FeatureName featureName) => featureName switch
    {
        FeatureName.Streaming => host.Wrapper.SupportsStreaming,
        FeatureName.ReasoningEffort => true,
        FeatureName.ModelDiscovery => true,
        FeatureName.ProviderOverride => true,
        FeatureName.ExtensionParameters => true,
        _ => false,
    };

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg) ?? new ConversationExecutionOptions();
        ValidateExecutionOptions(optionsArg, execution);
        var modelId = execution.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = execution.ReasoningEffort ?? options.ReasoningEffort;
        var (activity, telemetry) = ChatTelemetry.Start("GitHubCopilot", modelId, reasoning);
        using var scope = CreateRequestScope(telemetry);

        try
        {
            logger.LogInformation("GitHub Copilot request accepted. Stage=request accepted; RequestId={RequestId}; TraceId={TraceId}", telemetry.RequestId, telemetry.TraceId);
            logger.LogDebug("GitHub Copilot invocation built. Stage=invocation built; ModelId={ModelId}; StreamingRequested={StreamingRequested}; TimeoutSeconds={TimeoutSeconds}", modelId, execution.Streaming ?? options.Streaming, execution.TimeoutSeconds ?? options.TimeoutSeconds);
            LogProviderOverride(execution.ProviderOverride ?? options.ProviderOverride);

            logger.LogInformation("GitHub Copilot model list start. Stage=model list start; RequestId={RequestId}", telemetry.RequestId);
            var models = await ListModelsAsync(cancellationToken);
            logger.LogInformation("GitHub Copilot model list completed. Stage=model list completed; Count={ModelCount}; RequestId={RequestId}", models.Count, telemetry.RequestId);
            var selected = models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                throw new InvalidRequestException($"Unknown GitHub Copilot model id '{modelId}'. Valid CLI model ids: {string.Join(", ", models.Select(static model => model.ModelId))}", "GitHubCopilot");
            }

            logger.LogInformation("GitHub Copilot selected model resolved. Stage=selected model resolved; ModelId={ModelId}; RequestId={RequestId}", selected.ModelId, telemetry.RequestId);
            if (reasoning is not null && !selected.SupportsReasoningEffort)
            {
                throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Reasoning effort is not supported by selected model.", "GitHubCopilot", "ReasoningEffort");
            }

            var config = new CopilotSessionConfig
            {
                ModelId = modelId,
                ReasoningEffort = reasoning,
                Streaming = execution.Streaming ?? options.Streaming,
                Attachments = execution.Attachments?.Select(static attachment => new FileAttachment
                {
                    Path = attachment.Path,
                    DisplayName = attachment.DisplayName,
                }).ToArray(),
                SkillDirectories = execution.SkillDirectories?.ToArray(),
                DisabledSkills = execution.DisabledSkills?.ToArray(),
                TimeoutSeconds = execution.TimeoutSeconds,
                ProviderOverride = execution.ProviderOverride ?? options.ProviderOverride,
                TraceId = telemetry.TraceId,
                RequestId = telemetry.RequestId,
            };

            ValidateExtensions(optionsArg, config);

            var prompt = string.Join("\n", messages.Select(FormatMessage));
            var text = await host.Wrapper.SendAsync(prompt, config, cancellationToken);
            logger.LogInformation("GitHub Copilot final response received. Stage=final response received; CharacterCount={CharacterCount}; RequestId={RequestId}", text.Length, telemetry.RequestId);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("GitHub Copilot request cancelled. Stage=cancellation; RequestId={RequestId}", telemetry.RequestId);
            throw;
        }
        catch (System.TimeoutException ex)
        {
            logger.LogWarning(ex, "GitHub Copilot request timed out. Stage=timeout; RequestId={RequestId}", telemetry.RequestId);
            throw;
        }
        catch (CopilotRuntimeException)
        {
            logger.LogError("GitHub Copilot runtime failure observed. Stage=runtime exception; RequestId={RequestId}", telemetry.RequestId);
            throw;
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            logger.LogError("GitHub Copilot exception will be wrapped. Stage=exception wrapped into CopilotRuntimeException; RequestId={RequestId}", telemetry.RequestId);
            logger.LogExceptionWithTrace(ex, telemetry.TraceId);
            throw new CopilotRuntimeException(
                "Failed to execute Copilot chat request.",
                "GitHubCopilot",
                options.CliPath,
                null,
                telemetry.TraceId,
                ex,
                CopilotOperation.Send);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? optionsArg = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!host.Wrapper.SupportsStreaming)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException(
                "Streaming is not supported by GitHubCopilot provider in the current SDK wrapper configuration.",
                "GitHubCopilot",
                "Streaming");
        }

        var execution = ConversationExecutionOptions.FromChatOptions(optionsArg) ?? new ConversationExecutionOptions();
        ValidateExecutionOptions(optionsArg, execution);
        var modelId = execution.ModelId ?? options.ModelId ?? "gpt-5";
        var reasoning = execution.ReasoningEffort ?? options.ReasoningEffort;
        var (activity, telemetry) = ChatTelemetry.Start("GitHubCopilot", modelId, reasoning);
        using var scope = CreateRequestScope(telemetry);

        var firstEventLogged = false;
        var firstDeltaLogged = false;
        var deltaCount = 0;
        var emittedDelta = false;
        var accumulatedLength = 0;

        try
        {
            logger.LogInformation("GitHub Copilot request accepted. Stage=request accepted; RequestId={RequestId}; TraceId={TraceId}", telemetry.RequestId, telemetry.TraceId);
            logger.LogInformation("GitHub Copilot model list start. Stage=model list start; RequestId={RequestId}", telemetry.RequestId);
            var models = await ListModelsAsync(cancellationToken);
            logger.LogInformation("GitHub Copilot model list completed. Stage=model list completed; Count={ModelCount}; RequestId={RequestId}", models.Count, telemetry.RequestId);
            var selected = models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                throw new InvalidRequestException($"Unknown GitHub Copilot model id '{modelId}'. Valid CLI model ids: {string.Join(", ", models.Select(static model => model.ModelId))}", "GitHubCopilot");
            }

            logger.LogInformation("GitHub Copilot selected model resolved. Stage=selected model resolved; ModelId={ModelId}; RequestId={RequestId}", selected.ModelId, telemetry.RequestId);
            if (reasoning is not null && !selected.SupportsReasoningEffort)
            {
                throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException("Reasoning effort is not supported by selected model.", "GitHubCopilot", "ReasoningEffort");
            }

            var config = new CopilotSessionConfig
            {
                ModelId = modelId,
                ReasoningEffort = reasoning,
                Streaming = true,
                Attachments = execution.Attachments?.Select(static attachment => new FileAttachment
                {
                    Path = attachment.Path,
                    DisplayName = attachment.DisplayName,
                }).ToArray(),
                SkillDirectories = execution.SkillDirectories?.ToArray(),
                DisabledSkills = execution.DisabledSkills?.ToArray(),
                TimeoutSeconds = execution.TimeoutSeconds,
                ProviderOverride = execution.ProviderOverride ?? options.ProviderOverride,
                TraceId = telemetry.TraceId,
                RequestId = telemetry.RequestId,
            };

            ValidateExtensions(optionsArg, config);
            LogProviderOverride(config.ProviderOverride);

            var prompt = string.Join("\n", messages.Select(FormatMessage));
            await foreach (var update in host.Wrapper.SendStreamingAsync(prompt, config, cancellationToken))
            {
                if (!firstEventLogged)
                {
                    firstEventLogged = true;
                    logger.LogInformation("GitHub Copilot first SDK event received. Stage=first SDK event received; RequestId={RequestId}", telemetry.RequestId);
                }

                if (update.Kind == CopilotStreamingUpdateKind.Delta && !string.IsNullOrEmpty(update.TextDelta))
                {
                    deltaCount++;
                    accumulatedLength += update.TextDelta.Length;
                    if (!firstDeltaLogged)
                    {
                        firstDeltaLogged = true;
                        logger.LogInformation("GitHub Copilot first response delta received. Stage=first response delta received; RequestId={RequestId}", telemetry.RequestId);
                    }

                    if (deltaCount % 5 == 0)
                    {
                        logger.LogDebug("GitHub Copilot streaming progress. Stage=response delta progress; DeltaCount={DeltaCount}; AccumulatedLength={AccumulatedLength}; RequestId={RequestId}", deltaCount, accumulatedLength, telemetry.RequestId);
                    }

                    emittedDelta = true;
                    yield return new ChatResponseUpdate(ChatRole.Assistant, update.TextDelta);
                }
                else if (update.Kind == CopilotStreamingUpdateKind.Progress)
                {
                    logger.LogDebug("GitHub Copilot streaming heartbeat. Stage=progress heartbeat; DeltaCount={DeltaCount}; AccumulatedLength={AccumulatedLength}; RequestId={RequestId}", update.DeltaCount ?? deltaCount, update.AccumulatedLength ?? accumulatedLength, telemetry.RequestId);
                }
                else if (update.Kind == CopilotStreamingUpdateKind.Completed)
                {
                    if (!emittedDelta && !string.IsNullOrWhiteSpace(update.FinalText))
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, update.FinalText);
                    }

                    logger.LogInformation("GitHub Copilot final response received. Stage=final response received; DeltaCount={DeltaCount}; AccumulatedLength={AccumulatedLength}; RequestId={RequestId}", deltaCount, accumulatedLength, telemetry.RequestId);
                }
            }
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
        => host.ListModelsAsync(cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType switch
    {
        _ when serviceType == typeof(IProviderCapabilities) => this,
        _ when serviceType == typeof(ICopilotModelCatalog) => this,
        _ => null,
    };
    public void Dispose() { }

    private static void ValidateExecutionOptions(ChatOptions? optionsArg, ConversationExecutionOptions execution)
    {
        if (optionsArg?.ResponseFormat is not null)
        {
            throw new MeAiUtility.MultiProvider.Exceptions.NotSupportedException(
                "ResponseFormat is not supported by GitHubCopilot provider.",
                "GitHubCopilot",
                "ResponseFormat");
        }

        if (execution.TimeoutSeconds is <= 0)
        {
            throw new InvalidRequestException("TimeoutSeconds must be greater than zero.", "GitHubCopilot");
        }

        if (execution.Attachments is null)
        {
            return;
        }

        foreach (var attachment in execution.Attachments)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Path))
            {
                throw new InvalidRequestException("Attachment path must be specified.", "GitHubCopilot");
            }

            if (!Path.IsPathRooted(attachment.Path))
            {
                throw new InvalidRequestException("Attachment path must be an absolute path.", "GitHubCopilot");
            }
        }
    }

    private static void ValidateExtensions(ChatOptions? optionsArg, CopilotSessionConfig config)
    {
        if (optionsArg?.AdditionalProperties is not null
            && optionsArg.AdditionalProperties.TryGetValue("meai.extensions", out var raw))
        {
            if (raw is not ExtensionParameters ext)
            {
                throw new InvalidRequestException("ChatOptions meai.extensions must be ExtensionParameters.", "GitHubCopilot");
            }

            foreach (var kv in ext.GetAllForProvider("copilot"))
            {
                if (ShouldIgnoreExtensionByTypedOverride(kv.Key, config))
                {
                    continue;
                }

                config.AdvancedOptions[kv.Key] = kv.Value;
            }

            var disallowed = ext.GetAllForProvider("openai").Concat(ext.GetAllForProvider("azure")).ToArray();
            if (disallowed.Length > 0)
            {
                throw new InvalidRequestException("Unsupported extension prefix for provider.", "GitHubCopilot");
            }
        }

        config.SkillDirectories ??= GetOptionalStringList(config.AdvancedOptions, "copilot.skillDirectories", "copilot.skill_directories");
        config.DisabledSkills ??= GetOptionalStringList(config.AdvancedOptions, "copilot.disabledSkills", "copilot.disabled_skills");
    }

    private static bool ShouldIgnoreExtensionByTypedOverride(string key, CopilotSessionConfig config)
    {
        if (config.SkillDirectories is not null &&
            (string.Equals(key, "copilot.skillDirectories", StringComparison.OrdinalIgnoreCase)
             || string.Equals(key, "copilot.skill_directories", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (config.DisabledSkills is not null &&
            (string.Equals(key, "copilot.disabledSkills", StringComparison.OrdinalIgnoreCase)
             || string.Equals(key, "copilot.disabled_skills", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
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

            throw new InvalidRequestException($"Extension '{key}' must be an array of strings.", "GitHubCopilot");
        }

        return null;
    }

    private static string FormatMessage(ChatMessage message) => $"{GetRoleDisplayName(message.Role)}: {message.Text}";

    private IDisposable? CreateRequestScope(ChatTelemetry telemetry)
    {
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = telemetry.TraceId,
            ["RequestId"] = telemetry.RequestId,
            ["Provider"] = "GitHubCopilot",
        });
    }

    private void LogProviderOverride(ProviderOverrideOptions? providerOverride)
    {
        if (providerOverride is null)
        {
            return;
        }

        logger.LogDebug(
            "GitHub Copilot provider override applied. Type={Type}; BaseUrl={BaseUrl}; AzureApiVersion={AzureApiVersion}; HasApiKey={HasApiKey}; HasBearerToken={HasBearerToken}",
            providerOverride.Type,
            providerOverride.BaseUrl,
            providerOverride.AzureApiVersion,
            !string.IsNullOrWhiteSpace(providerOverride.ApiKey),
            !string.IsNullOrWhiteSpace(providerOverride.BearerToken));
    }

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
