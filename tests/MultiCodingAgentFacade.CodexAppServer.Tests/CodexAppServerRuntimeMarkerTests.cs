using MultiCodingAgentFacade.CodexAppServer;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Stdio;
using MultiCodingAgentFacade.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MultiCodingAgentFacade.CodexAppServer.Tests;

public sealed class CodexAppServerRuntimeMarkerTests
{
    [Fact]
    public void RuntimeNameIsCodexAppServer()
    {
        Assert.Equal("CodexAppServer", CodexAppServerRuntimeMarker.RuntimeName);
    }

    [Fact]
    public void StdioTransportValidationUsesRuntimeMarkerName()
    {
        var exception = Assert.Throws<RuntimeInvalidRequestException>(() =>
            new StdioCodexTransport(
                new ThrowingCodexProcessRunner(),
                NullLogger<StdioCodexTransport>.Instance,
                new CodexAppServerOptions { CodexArguments = ["app-server", "app-server"] },
                workingDirectory: null));

        Assert.Equal(CodexAppServerRuntimeMarker.RuntimeName, exception.RuntimeName);
    }

    private sealed class ThrowingCodexProcessRunner : ICodexProcessRunner
    {
        public Task<System.Diagnostics.Process> StartAsync(CodexProcessStartInfo startInfo, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The process runner should not be used by constructor validation tests.");
    }
}
