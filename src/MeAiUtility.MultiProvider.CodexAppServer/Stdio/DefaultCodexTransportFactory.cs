using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.CodexAppServer.Stdio;

public sealed class DefaultCodexTransportFactory(
    ICodexProcessRunner processRunner,
    ILoggerFactory loggerFactory,
    CodexAppServerProviderOptions providerOptions) : ICodexTransportFactory
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
