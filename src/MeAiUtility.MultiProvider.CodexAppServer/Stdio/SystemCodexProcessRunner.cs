using System.Diagnostics;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Stdio;

public sealed class SystemCodexProcessRunner : ICodexProcessRunner
{
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
            FileName = startInfo.Command,
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
}
