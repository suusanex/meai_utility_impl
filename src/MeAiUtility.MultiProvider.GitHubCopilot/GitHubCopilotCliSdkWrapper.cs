using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed partial class GitHubCopilotCliSdkWrapper(GitHubCopilotProviderOptions options) : ICopilotSdkWrapper
{
    private static readonly TimeSpan CommandResolutionTimeout = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCopilotAsync(["help", "config"], cancellationToken);
        var match = ConfigModelsSectionRegex().Match(result.StandardOutput);
        var models = QuotedValueRegex().Matches(match.Success ? match.Groups["choices"].Value : string.Empty)
            .Select(static m => m.Groups["value"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(model => new CopilotModelInfo(model, SupportsReasoningEffort(model)))
            .ToArray();

        if (models.Length > 0)
        {
            return models;
        }

        throw new InvalidOperationException("Failed to discover GitHub Copilot model ids from 'copilot help config'.");
    }

    public async Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(config);

        if (config.ReasoningEffort is not null)
        {
            throw new InvalidOperationException("ReasoningEffort is not supported by this CLI wrapper.");
        }

        if (config.ProviderOverride is not null)
        {
            throw new InvalidOperationException("ProviderOverride is not supported by this CLI wrapper.");
        }

        if (config.AdvancedOptions.Count > 0)
        {
            throw new InvalidOperationException($"Advanced option '{config.AdvancedOptions.Keys.First()}' is not supported by this CLI wrapper.");
        }

        var commandArguments = new List<string>();
        if (options.CliArgs is not null)
        {
            commandArguments.AddRange(options.CliArgs);
        }

        commandArguments.AddRange(["-s", "--allow-all-tools"]);

        if (!string.IsNullOrWhiteSpace(config.ModelId))
        {
            commandArguments.Add("--model");
            commandArguments.Add(config.ModelId);
        }

        if (!string.IsNullOrWhiteSpace(options.LogLevel))
        {
            commandArguments.Add("--log-level");
            commandArguments.Add(options.LogLevel);
        }

        if (config.Streaming.HasValue)
        {
            commandArguments.Add("--stream");
            commandArguments.Add(config.Streaming.Value ? "on" : "off");
        }

        if (!string.IsNullOrWhiteSpace(options.ConfigDir))
        {
            commandArguments.Add("--config-dir");
            commandArguments.Add(options.ConfigDir);
        }

        if (options.AvailableTools is { Count: > 0 })
        {
            commandArguments.Add("--available-tools");
            commandArguments.AddRange(options.AvailableTools);
        }

        if (options.ExcludedTools is { Count: > 0 })
        {
            commandArguments.Add("--excluded-tools");
            commandArguments.AddRange(options.ExcludedTools);
        }

        var resolution = ResolveCommand();
        ProcessResult result;
        if (string.Equals(resolution.FileName, "powershell.exe", StringComparison.OrdinalIgnoreCase)
            && resolution.CommandPrefix.Count > 0
            && resolution.CommandPrefix.Last().EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            result = await RunPowerShellPromptCopilotAsync(resolution.ResolvedPath, prompt, commandArguments, cancellationToken);
        }
        else
        {
            commandArguments.Insert(0, prompt);
            commandArguments.Insert(0, "-p");
            result = await RunCopilotAsync(resolution, commandArguments, cancellationToken);
        }

        return result.StandardOutput.Trim();
    }

    private async Task<ProcessResult> RunCopilotAsync(IReadOnlyCollection<string> arguments, CancellationToken cancellationToken)
    {
        return await RunCopilotAsync(ResolveCommand(), arguments, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessResult> RunCopilotAsync(
        ResolvedCommand resolution,
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        if (options.UseLoggedInUser is false && string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            throw new InvalidOperationException("GitHubToken is required when UseLoggedInUser is false.");
        }

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            throw new InvalidOperationException("CliUrl is not supported by this CLI wrapper.");
        }

        return await RunProcessAsync(
            resolution.FileName,
            resolution.CommandPrefix,
            arguments,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessResult> RunPowerShellPromptCopilotAsync(
        string resolvedScriptPath,
        string prompt,
        IReadOnlyCollection<string> commandArguments,
        CancellationToken cancellationToken)
    {
        var promptFilePath = Path.Combine(Path.GetTempPath(), $"copilot-prompt-{Guid.NewGuid():N}.txt");
        var bootstrapFilePath = Path.Combine(Path.GetTempPath(), $"copilot-bootstrap-{Guid.NewGuid():N}.ps1");

        try
        {
            await File.WriteAllTextAsync(promptFilePath, prompt, new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(
                bootstrapFilePath,
                """
param(
    [string]$CopilotScriptPath,
    [string]$PromptFilePath
)

$prompt = Get-Content -LiteralPath $PromptFilePath -Raw
& $CopilotScriptPath -p $prompt @args
exit $LASTEXITCODE
""",
                new UTF8Encoding(false),
                cancellationToken);

            var invocationArguments = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                bootstrapFilePath,
                resolvedScriptPath,
                promptFilePath,
            };
            invocationArguments.AddRange(commandArguments);

            return await RunProcessAsync(
                "powershell.exe",
                [],
                invocationArguments,
                cancellationToken);
        }
        finally
        {
            TryDelete(promptFilePath);
            TryDelete(bootstrapFilePath);
        }
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyCollection<string> commandPrefix,
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory) ? Environment.CurrentDirectory : options.WorkingDirectory,
        };

        foreach (var prefix in commandPrefix)
        {
            startInfo.ArgumentList.Add(prefix);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (options.EnvironmentVariables is not null)
        {
            foreach (var entry in options.EnvironmentVariables)
            {
                startInfo.Environment[entry.Key] = entry.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            startInfo.Environment["COPILOT_GITHUB_TOKEN"] = options.GitHubToken;
            startInfo.Environment["GH_TOKEN"] = options.GitHubToken;
            startInfo.Environment["GITHUB_TOKEN"] = options.GitHubToken;
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the Copilot CLI process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 1)));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw new TimeoutException($"Copilot CLI timed out after {options.TimeoutSeconds} seconds.");
        }

        var standardOutput = await stdoutTask;
        var standardError = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Copilot CLI exited with code {process.ExitCode}: {standardError.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(standardOutput) && !string.IsNullOrWhiteSpace(standardError))
        {
            throw new InvalidOperationException($"Copilot CLI returned no output: {standardError.Trim()}");
        }

        return new ProcessResult(standardOutput, standardError);
    }

    private static void TryTerminate(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Trace.TraceError($"Failed to delete temporary file '{path}'. Exception: {ex.ToString()}");
        }
    }

    private static bool SupportsReasoningEffort(string modelId)
    {
        return modelId.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || modelId.StartsWith("claude-sonnet", StringComparison.OrdinalIgnoreCase)
            || modelId.StartsWith("claude-opus", StringComparison.OrdinalIgnoreCase);
    }

    private ResolvedCommand ResolveCommand()
    {
        var configuredCliPath = string.IsNullOrWhiteSpace(options.CliPath) ? "copilot" : options.CliPath;
        var resolvedPath = ResolveCliPath(configuredCliPath);
        return ResolveCommandFromResolvedPath(resolvedPath);
    }

    private ResolvedCommand ResolveCommandFromResolvedPath(string resolvedPath)
    {
        var nativeResolvedCommand = TryResolveNativeCommandFromPowerShellShim(resolvedPath);
        if (nativeResolvedCommand is not null)
        {
            return nativeResolvedCommand;
        }

        if (resolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedCommand(resolvedPath, "powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolvedPath]);
        }

        if (resolvedPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            resolvedPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedCommand(resolvedPath, "cmd.exe", ["/c", resolvedPath]);
        }

        return new ResolvedCommand(resolvedPath, resolvedPath, []);
    }

    private ResolvedCommand? TryResolveNativeCommandFromPowerShellShim(string resolvedPath)
    {
        if (!resolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || !File.Exists(resolvedPath))
        {
            return null;
        }

        var scriptText = File.ReadAllText(resolvedPath);

        if (scriptText.Contains("node_modules/@github/copilot/npm-loader.js", StringComparison.Ordinal))
        {
            var baseDirectory = Path.GetDirectoryName(resolvedPath)
                ?? throw new InvalidOperationException($"Failed to resolve directory for '{resolvedPath}'.");
            var loaderPath = Path.Combine(baseDirectory, "node_modules", "@github", "copilot", "npm-loader.js");
            if (!File.Exists(loaderPath))
            {
                throw new InvalidOperationException($"Failed to locate GitHub Copilot npm loader next to '{resolvedPath}'.");
            }

            var nodeFileName = TryResolveNodeCommand(baseDirectory);
            if (!string.IsNullOrWhiteSpace(nodeFileName))
            {
                return new ResolvedCommand(resolvedPath, nodeFileName, [loaderPath]);
            }

            return null;
        }

        if (scriptText.Contains("Windows GitHub Copilot CLI bootstrapper", StringComparison.Ordinal))
        {
            var scriptDirectory = Path.GetDirectoryName(resolvedPath)
                ?? throw new InvalidOperationException($"Failed to resolve directory for '{resolvedPath}'.");
            var downstreamPath = ResolveCliPathExcludingDirectory("copilot", scriptDirectory);
            if (!string.Equals(downstreamPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCommandFromResolvedPath(downstreamPath);
            }
        }

        return null;
    }

    private static string ResolveCliPath(string configuredCliPath)
    {
        if (Path.IsPathFullyQualified(configuredCliPath) && File.Exists(configuredCliPath))
        {
            return configuredCliPath;
        }

        if (!OperatingSystem.IsWindows())
        {
            return configuredCliPath;
        }

        return RunResolutionProcess(
            configuredCliPath,
            [
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                $"(Get-Command '{EscapePowerShellSingleQuotedString(configuredCliPath)}').Source",
            ]);
    }

    private static string ResolveCliPathExcludingDirectory(string configuredCliPath, string excludedDirectory)
    {
        var escapedExcludedDirectory = EscapePowerShellSingleQuotedString(excludedDirectory);

        return RunResolutionProcess(
            configuredCliPath,
            [
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                $$"""
$oldPath = $env:PATH
$env:PATH = (($env:PATH -split ';') | Where-Object { $_ -ne '{{escapedExcludedDirectory}}' }) -join ';'
try {
    (Get-Command '{{EscapePowerShellSingleQuotedString(configuredCliPath)}}').Source
}
finally {
    $env:PATH = $oldPath
}
""",
            ]);
    }

    private static string? TryResolveNodeCommand(string baseDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var localNodePath = Path.Combine(baseDirectory, "node.exe");
        if (File.Exists(localNodePath))
        {
            return localNodePath;
        }

        try
        {
            return ResolveCliPath("node.exe");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to resolve optional node.exe for '{baseDirectory}'. Exception: {ex.ToString()}");
            return null;
        }
    }

    private static string RunResolutionProcess(string configuredCliPath, IReadOnlyCollection<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to resolve CLI path for '{configuredCliPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)CommandResolutionTimeout.TotalMilliseconds))
        {
            TryTerminate(process);
            throw new TimeoutException($"CLI path resolution timed out for '{configuredCliPath}'.");
        }

        process.WaitForExit();
        Task.WhenAll(stdoutTask, stderrTask).GetAwaiter().GetResult();

        var standardOutput = stdoutTask.Result.Trim();
        var standardError = stderrTask.Result.Trim();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new InvalidOperationException($"Failed to resolve CLI path for '{configuredCliPath}': {standardError}");
        }

        return standardOutput;
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    [GeneratedRegex("^\\s*`model`:\\s+.*?(?<choices>(?:^\\s+-\\s+\"[^\"]+\"\\s*$\\r?\\n?)+)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex ConfigModelsSectionRegex();

    [GeneratedRegex("\"(?<value>[^\"]+)\"")]
    private static partial Regex QuotedValueRegex();

    private sealed record ResolvedCommand(string ResolvedPath, string FileName, IReadOnlyList<string> CommandPrefix);
    private sealed record ProcessResult(string StandardOutput, string StandardError);
}
