using MultiCodingAgentFacade.Core.Exceptions;

namespace MultiCodingAgentFacade.GitHubCopilot;

public enum CopilotOperation
{
    ClientInitialization,
    ListModels,
    Send,
}

public sealed class CopilotRuntimeException : RuntimeFacadeException
{
    public CopilotRuntimeException(
        string message,
        string runtimeName,
        string? cliPath = null,
        string? commandLine = null,
        string? runtimeConnection = null,
        string? traceId = null,
        Exception? innerException = null,
        CopilotOperation? operation = null)
        : base(message, runtimeName, traceId, null, null, innerException)
    {
        CliPath = cliPath;
        CommandLine = commandLine;
        RuntimeConnection = runtimeConnection;
        Operation = operation;
    }

    public string? CliPath { get; }
    public string? CommandLine { get; }
    public string? RuntimeConnection { get; }
    public CopilotOperation? Operation { get; }
}
