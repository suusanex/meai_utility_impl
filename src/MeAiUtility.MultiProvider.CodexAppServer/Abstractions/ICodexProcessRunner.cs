using System.Diagnostics;

namespace MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

public sealed class CodexProcessStartInfo
{
    public string Command { get; set; } = "codex";
    public IReadOnlyList<string> Arguments { get; set; } = ["app-server"];
    public string? WorkingDirectory { get; set; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; set; }
}

public interface ICodexProcessRunner
{
    Task<Process> StartAsync(CodexProcessStartInfo startInfo, CancellationToken cancellationToken = default);
}
