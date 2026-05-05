namespace MeAiUtility.MultiProvider.CodexAppServer.Abstractions;

public interface ICodexTransportFactory
{
    ICodexTransport Create(string? workingDirectory);
}
