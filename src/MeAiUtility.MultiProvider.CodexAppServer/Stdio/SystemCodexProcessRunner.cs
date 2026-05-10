using System.Diagnostics;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Stdio;

public sealed class SystemCodexProcessRunner : ICodexProcessRunner
{
    private static readonly string[] WindowsExecutableExtensions = [".exe", ".cmd", ".bat", ".com"];

    public Task<Process> StartAsync(CodexProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(startInfo.Command))
        {
            throw new InvalidOperationException("Codex command path must be configured.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = ResolveCommand(startInfo),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in startInfo.Arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
        {
            psi.WorkingDirectory = startInfo.WorkingDirectory;
        }

        if (startInfo.EnvironmentVariables is not null)
        {
            foreach (var kv in startInfo.EnvironmentVariables)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start codex app-server process.");
        }

        return Task.FromResult(process);
    }

    private static string ResolveCommand(CodexProcessStartInfo startInfo)
    {
        var command = startInfo.Command.Trim();
        if (!OperatingSystem.IsWindows())
        {
            return command;
        }

        if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return ResolveCommandFromExplicitPath(command);
        }

        if (Path.HasExtension(command))
        {
            return command;
        }

        var pathValue = GetPathFromEnvironmentVariables(startInfo.EnvironmentVariables)
            ?? Environment.GetEnvironmentVariable("PATH")
            ?? string.Empty;

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in WindowsExecutableExtensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    private static string ResolveCommandFromExplicitPath(string command)
    {
        if (Path.HasExtension(command) || File.Exists(command))
        {
            return command;
        }

        foreach (var extension in WindowsExecutableExtensions)
        {
            var candidate = command + extension;
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return command;
    }

    private static string? GetPathFromEnvironmentVariables(IReadOnlyDictionary<string, string>? environmentVariables)
    {
        if (environmentVariables is null)
        {
            return null;
        }

        foreach (var entry in environmentVariables)
        {
            if (string.Equals(entry.Key, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }
}
