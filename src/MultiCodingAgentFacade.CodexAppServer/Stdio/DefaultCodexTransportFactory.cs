using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using Microsoft.Extensions.Logging;

namespace MultiCodingAgentFacade.CodexAppServer.Stdio;

public sealed class DefaultCodexTransportFactory(
    ICodexProcessRunner processRunner,
    ILoggerFactory loggerFactory,
    CodexAppServerOptions providerOptions) : ICodexTransportFactory
{
    public ICodexTransport Create(string? workingDirectory)
    {
        return new StdioCodexTransport(
            processRunner,
            loggerFactory.CreateLogger<StdioCodexTransport>(),
            providerOptions,
            workingDirectory);
    }
}
