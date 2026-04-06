extern alias GitHubCopilotSdk;

using System.Text.Json;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.Logging;
using CopilotSdk = GitHubCopilotSdk::GitHub.Copilot.SDK;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class GitHubCopilotSdkWrapper : ICopilotSdkWrapper, IDisposable, IAsyncDisposable
{
    private readonly GitHubCopilotProviderOptions options;
    private readonly ILogger<GitHubCopilotSdkWrapper> logger;
    private readonly SemaphoreSlim clientLock = new(1, 1);
    private readonly Func<CancellationToken, Task<IReadOnlyList<CopilotModelInfo>>>? listModelsCore;
    private readonly Func<CopilotSdkInvocation, CancellationToken, Task<string>>? sendCore;
    private CopilotSdk.CopilotClient? client;
    private bool disposed;

    public GitHubCopilotSdkWrapper(GitHubCopilotProviderOptions options, ILogger<GitHubCopilotSdkWrapper> logger)
        : this(options, logger, null, null)
    {
    }

    internal GitHubCopilotSdkWrapper(
        GitHubCopilotProviderOptions options,
        ILogger<GitHubCopilotSdkWrapper> logger,
        Func<CancellationToken, Task<IReadOnlyList<CopilotModelInfo>>>? listModelsCore,
        Func<CopilotSdkInvocation, CancellationToken, Task<string>>? sendCore)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.listModelsCore = listModelsCore;
        this.sendCore = sendCore;
    }

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
                model.SupportedReasoningEfforts is { Count: > 0 }))
        ];
    }

    public async Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);

        var invocation = BuildInvocation(prompt, config, options);
        if (sendCore is not null)
        {
            return await sendCore(invocation, cancellationToken).ConfigureAwait(false);
        }

        var sdkClient = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        await using var session = await sdkClient.CreateSessionAsync(BuildSdkSessionConfig(invocation), cancellationToken).ConfigureAwait(false);
        var response = await session.SendAndWaitAsync(
                new CopilotSdk.MessageOptions
                {
                    Prompt = invocation.Prompt,
                    Attachments = BuildMessageAttachments(invocation.Attachments),
                    Mode = invocation.Mode,
                },
                TimeSpan.FromSeconds(invocation.TimeoutSeconds),
                cancellationToken)
            .ConfigureAwait(false);

        var text = response?.Data?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("GitHub Copilot SDK returned no output.");
        }

        return text;
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

    internal static CopilotSdkInvocation BuildInvocation(string prompt, CopilotSessionConfig config, GitHubCopilotProviderOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);

        var authOptions = ResolveClientAuthOptions(options);
        if (authOptions.UseLoggedInUser is false && string.IsNullOrWhiteSpace(authOptions.GitHubToken))
        {
            throw new InvalidOperationException("GitHubToken is required when UseLoggedInUser is false.");
        }

        var mode = GetOptionalString(config.AdvancedOptions, "copilot.mode", "copilot.messageMode");
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
            throw new InvalidRequestException("TimeoutSeconds must be greater than zero.", "GitHubCopilot");
        }

        ValidateAttachments(config.Attachments);

        EnsureSupportedAdvancedOptions(config.AdvancedOptions);

        return new CopilotSdkInvocation(
            prompt,
            mode,
            config.ModelId ?? options.ModelId,
            MapReasoningEffort(config.ReasoningEffort ?? options.ReasoningEffort),
            config.Streaming ?? options.Streaming ?? false,
            configDir,
            workingDirectory,
            options.ClientName,
            availableTools,
            excludedTools,
            config.ProviderOverride ?? options.ProviderOverride,
            options.InfiniteSessions,
            mcpServers,
            agent,
            skillDirectories,
            disabledSkills,
            config.Attachments?.ToArray(),
            timeoutSeconds);
    }

    private static CopilotSdk.SessionConfig BuildSdkSessionConfig(CopilotSdkInvocation invocation)
    {
        return new CopilotSdk.SessionConfig
        {
            Model = invocation.ModelId,
            ReasoningEffort = invocation.ReasoningEffort,
            Streaming = invocation.Streaming,
            ConfigDir = invocation.ConfigDir,
            WorkingDirectory = invocation.WorkingDirectory,
            ClientName = invocation.ClientName,
            AvailableTools = invocation.AvailableTools?.ToList(),
            ExcludedTools = invocation.ExcludedTools?.ToList(),
            Provider = invocation.ProviderOverride is null ? null : new CopilotSdk.ProviderConfig
            {
                Type = invocation.ProviderOverride.Type,
                BaseUrl = invocation.ProviderOverride.BaseUrl,
                ApiKey = invocation.ProviderOverride.ApiKey,
                BearerToken = invocation.ProviderOverride.BearerToken,
                Azure = string.IsNullOrWhiteSpace(invocation.ProviderOverride.AzureApiVersion)
                    ? null
                    : new CopilotSdk.AzureOptions { ApiVersion = invocation.ProviderOverride.AzureApiVersion },
            },
            InfiniteSessions = invocation.InfiniteSessions is null ? null : new CopilotSdk.InfiniteSessionConfig
            {
                Enabled = invocation.InfiniteSessions.Enabled,
                BackgroundCompactionThreshold = invocation.InfiniteSessions.BackgroundCompactionThreshold,
                BufferExhaustionThreshold = invocation.InfiniteSessions.BufferExhaustionThreshold,
            },
            McpServers = invocation.McpServers is null ? null : new Dictionary<string, object>(invocation.McpServers, StringComparer.Ordinal),
            Agent = invocation.Agent,
            SkillDirectories = invocation.SkillDirectories?.ToList(),
            DisabledSkills = invocation.DisabledSkills?.ToList(),
            OnPermissionRequest = CopilotSdk.PermissionHandler.ApproveAll,
        };
    }

    private async Task<CopilotSdk.CopilotClient> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var existing = client;
        if (existing is not null)
        {
            return existing;
        }

        await clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (client is not null)
            {
                return client;
            }

            var authOptions = ResolveClientAuthOptions(options);
            try
            {
                var clientOptions = new CopilotSdk.CopilotClientOptions
                {
                    CliPath = options.CliPath,
                    CliArgs = options.CliArgs?.ToArray(),
                    Cwd = options.WorkingDirectory,
                    CliUrl = options.CliUrl,
                    UseStdio = options.UseStdio,
                    LogLevel = options.LogLevel,
                    AutoStart = options.AutoStart,
                    GitHubToken = authOptions.GitHubToken,
                    UseLoggedInUser = authOptions.UseLoggedInUser,
                    Environment = options.EnvironmentVariables,
                    Logger = logger,
                };
#pragma warning disable CS0618
                clientOptions.AutoRestart = options.AutoRestart;
#pragma warning restore CS0618
                client = new CopilotSdk.CopilotClient(clientOptions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not CopilotRuntimeException)
            {
                throw BuildClientInitializationException(ex);
            }

            return client;
        }
        finally
        {
            clientLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    internal static CopilotClientAuthOptions ResolveClientAuthOptions(GitHubCopilotProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            return new CopilotClientAuthOptions(options.GitHubToken, false);
        }

        return new CopilotClientAuthOptions(null, options.UseLoggedInUser);
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
            traceId,
            ex,
            CopilotOperation.ClientInitialization);
    }

    internal string BuildCliDiagnosticsSummary()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = GetPathEntries(path);
        var knownLocations = GetKnownCliLocations();
        return $"OS={Environment.OSVersion.VersionString}; CliPath={options.CliPath ?? "(not set)"}; PathEntryCount={pathEntries.Count}; KnownLocationCount={knownLocations.Count}";
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

    private static List<CopilotSdk.UserMessageDataAttachmentsItem>? BuildMessageAttachments(IReadOnlyList<FileAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return null;
        }

        return
        [
            .. attachments.Select(static attachment => (CopilotSdk.UserMessageDataAttachmentsItem)CreateSdkFileAttachment(attachment)),
        ];
    }

    private static CopilotSdk.UserMessageDataAttachmentsItemFile CreateSdkFileAttachment(FileAttachment attachment)
    {
        var path = ValidateAttachmentPath(attachment);
        var displayName = attachment.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = Path.GetFileName(path);
        }

        var sdkAttachment = new CopilotSdk.UserMessageDataAttachmentsItemFile
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
            throw new InvalidRequestException("Attachment path must be specified.", "GitHubCopilot");
        }

        if (!Path.IsPathRooted(attachment.Path))
        {
            throw new InvalidRequestException("Attachment path must be an absolute path.", "GitHubCopilot");
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

            throw new InvalidOperationException($"Advanced option '{key}' is not supported by this SDK wrapper.");
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

            throw new InvalidOperationException($"Advanced option '{key}' must be a non-empty string.");
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

            throw new InvalidOperationException($"Advanced option '{key}' must be an array of strings.");
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

            throw new InvalidOperationException($"Advanced option '{key}' must be an object.");
        }

        return null;
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
            _ => throw new InvalidOperationException($"Unsupported reasoning effort '{reasoningEffort}'."),
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
        catch (System.NotSupportedException ex)
        {
            throw CreateAdvancedOptionTypeException(key, expectedType, ex);
        }
    }

    private static InvalidOperationException CreateAdvancedOptionTypeException(string key, string expectedType, Exception innerException)
    {
        return new InvalidOperationException($"Advanced option '{key}' must be {expectedType}.", innerException);
    }

    private static readonly HashSet<string> SupportedAdvancedOptions = new(StringComparer.Ordinal)
    {
        "copilot.mode",
        "copilot.messageMode",
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
    string? ConfigDir,
    string? WorkingDirectory,
    string? ClientName,
    IReadOnlyList<string>? AvailableTools,
    IReadOnlyList<string>? ExcludedTools,
    ProviderOverrideOptions? ProviderOverride,
    InfiniteSessionOptions? InfiniteSessions,
    IReadOnlyDictionary<string, object>? McpServers,
    string? Agent,
    IReadOnlyList<string>? SkillDirectories,
    IReadOnlyList<string>? DisabledSkills,
    IReadOnlyList<FileAttachment>? Attachments,
    int TimeoutSeconds);

internal sealed record CopilotClientAuthOptions(
    string? GitHubToken,
    bool? UseLoggedInUser);
