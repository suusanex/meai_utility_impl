namespace MeAiUtility.MultiProvider.CodexAppServer;

public sealed class CodexProcessExitedException()
    : InvalidOperationException(MessageText)
{
    public const string MessageText = "Codex process exited unexpectedly.";
}
