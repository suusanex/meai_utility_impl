namespace MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

internal interface ICodexTransportDiagnostics
{
    string? CommandForDiagnostics { get; }
    IReadOnlyList<string> ArgumentsForDiagnostics { get; }
    int? ExitCodeForDiagnostics { get; }
    string? StderrTailForDiagnostics { get; }
}
