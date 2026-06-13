using System.Diagnostics;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Stdio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MultiCodingAgentFacade.CodexAppServer.Tests;

public sealed class StdioCodexTransportTests
{
    [Fact]
    public async Task ExitCodeForDiagnostics_DoesNotThrowAfterTransportDispose()
    {
        var transport = new StdioCodexTransport(
            new SleeperCodexProcessRunner(),
            NullLogger<StdioCodexTransport>.Instance,
            new CodexAppServerOptions { CodexCommand = "cmd", CodexArguments = [] },
            workingDirectory: null);

        await transport.StartAsync();
        await transport.DisposeAsync();

        var exception = Record.Exception(() => _ = transport.ExitCodeForDiagnostics);
        Assert.Null(exception);
    }

    private sealed class SleeperCodexProcessRunner : ICodexProcessRunner
    {
        public Task<Process> StartAsync(CodexProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--info",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;

            return Task.FromResult(process);
        }
    }
}
