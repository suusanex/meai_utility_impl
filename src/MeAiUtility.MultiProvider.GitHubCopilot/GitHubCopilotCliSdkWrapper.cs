using System.Diagnostics;
using System.Text.RegularExpressions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed partial class GitHubCopilotCliSdkWrapper(GitHubCopilotProviderOptions options) : ICopilotSdkWrapper
{
    public async Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCopilotAsync(["help", "config"], cancellationToken);
        var match = ConfigModelsSectionRegex().Match(result.StandardOutput);
        var models = QuotedValueRegex().Matches(match.Success ? match.Groups["choices"].Value : string.Empty)
            .Select(static m => m.Groups["value"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(model => new CopilotModelInfo(model, false))
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

        var arguments = new List<string>();
        if (options.CliArgs is not null)
        {
            arguments.AddRange(options.CliArgs);
        }

        arguments.AddRange(["-p", prompt, "-s", "--allow-all-tools"]);

        if (!string.IsNullOrWhiteSpace(config.ModelId))
        {
            arguments.Add("--model");
            arguments.Add(config.ModelId);
        }

        if (!string.IsNullOrWhiteSpace(options.LogLevel))
        {
            arguments.Add("--log-level");
            arguments.Add(options.LogLevel);
        }

        if (config.Streaming.HasValue)
        {
            arguments.Add("--stream");
            arguments.Add(config.Streaming.Value ? "on" : "off");
        }

        if (!string.IsNullOrWhiteSpace(options.ConfigDir))
        {
            arguments.Add("--config-dir");
            arguments.Add(options.ConfigDir);
        }

        if (options.AvailableTools is { Count: > 0 })
        {
            arguments.Add("--available-tools");
            arguments.AddRange(options.AvailableTools);
        }

        if (options.ExcludedTools is { Count: > 0 })
        {
            arguments.Add("--excluded-tools");
            arguments.AddRange(options.ExcludedTools);
        }

        var result = await RunCopilotAsync(arguments, cancellationToken);
        return result.StandardOutput.Trim();
    }

    private async Task<ProcessResult> RunCopilotAsync(IReadOnlyCollection<string> arguments, CancellationToken cancellationToken)
    {
        if (options.UseLoggedInUser is false && string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            throw new InvalidOperationException("GitHubToken is required when UseLoggedInUser is false.");
        }

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            throw new InvalidOperationException("CliUrl is not supported by this CLI wrapper.");
        }

        var (fileName, commandPrefix) = ResolveCommand();
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

    private (string FileName, IReadOnlyList<string> CommandPrefix) ResolveCommand()
    {
        var configuredCliPath = string.IsNullOrWhiteSpace(options.CliPath) ? "copilot" : options.CliPath;
        var resolvedPath = ResolveCliPath(configuredCliPath);

        if (resolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return ("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolvedPath]);
        }

        if (resolvedPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            resolvedPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return ("cmd.exe", ["/c", resolvedPath]);
        }

        return (resolvedPath, []);
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

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add($"(Get-Command '{configuredCliPath}').Source");

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to resolve CLI path for '{configuredCliPath}'.");
        }

        process.WaitForExit();
        var standardOutput = process.StandardOutput.ReadToEnd().Trim();
        var standardError = process.StandardError.ReadToEnd().Trim();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new InvalidOperationException($"Failed to resolve CLI path for '{configuredCliPath}': {standardError}");
        }

        return standardOutput;
    }

    [GeneratedRegex("^\\s*`model`:\\s+.*?(?<choices>(?:^\\s+-\\s+\"[^\"]+\"\\s*$\\r?\\n?)+)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex ConfigModelsSectionRegex();

    [GeneratedRegex("\"(?<value>[^\"]+)\"")]
    private static partial Regex QuotedValueRegex();

    private sealed record ProcessResult(string StandardOutput, string StandardError);
}
