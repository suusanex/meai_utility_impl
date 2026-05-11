namespace MeAiUtility.MultiProvider.CodexAppServer;

public sealed class CodexProcessExitedException : InvalidOperationException
{
    public const string MessageText = "Codex process exited unexpectedly.";

    public string? Command { get; }
    public IReadOnlyList<string> Arguments { get; }
    public int? ExitCode { get; }
    public string? StderrTail { get; }

    public CodexProcessExitedException()
        : this(null, null, null, null)
    {
    }

    public CodexProcessExitedException(string? command, IReadOnlyList<string>? arguments, int? exitCode, string? stderrTail)
        : base(BuildMessage(command, arguments, exitCode, stderrTail))
    {
        Command = command;
        Arguments = arguments ?? [];
        ExitCode = exitCode;
        StderrTail = stderrTail;
    }

    private static string BuildMessage(string? command, IReadOnlyList<string>? arguments, int? exitCode, string? stderrTail)
    {
        var commandText = string.IsNullOrWhiteSpace(command) ? "<unknown>" : command;
        var argsText = arguments is { Count: > 0 }
            ? string.Join(" ", arguments)
            : "<none>";
        var exitCodeText = exitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>";

        if (string.IsNullOrWhiteSpace(stderrTail))
        {
            return $"{MessageText} Command='{commandText}', Arguments='{argsText}', ExitCode={exitCodeText}.";
        }

        return $"{MessageText} Command='{commandText}', Arguments='{argsText}', ExitCode={exitCodeText}, StderrTail='{stderrTail}'.";
    }
}
