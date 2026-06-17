namespace MultiCodingAgentFacade.CodexAppServer.Abstractions;

public interface ICodexTransportDiagnostics
{
    string? CommandForDiagnostics { get; }
    IReadOnlyList<string> ArgumentsForDiagnostics { get; }
    int? ExitCodeForDiagnostics { get; }
    string? StderrTailForDiagnostics { get; }
}
