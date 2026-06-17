using System.Diagnostics;
using System.Text;
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

    [Fact]
    public async Task SystemCodexProcessRunner_StartAsyncSetsUtf8StreamEncodings()
    {
        var runner = new SystemCodexProcessRunner();
        var command = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/c", "exit", "0" }
            : new[] { "-c", "exit 0" };

        using var process = await runner.StartAsync(
            new CodexProcessStartInfo
            {
                Command = command,
                Arguments = arguments,
            },
            default);

        Assert.NotNull(process.StartInfo.StandardInputEncoding);
        Assert.NotNull(process.StartInfo.StandardOutputEncoding);
        Assert.NotNull(process.StartInfo.StandardErrorEncoding);
        Assert.IsType<UTF8Encoding>(process.StartInfo.StandardInputEncoding);
        Assert.IsType<UTF8Encoding>(process.StartInfo.StandardOutputEncoding);
        Assert.IsType<UTF8Encoding>(process.StartInfo.StandardErrorEncoding);
        Assert.Equal("utf-8", process.StartInfo.StandardInputEncoding!.WebName);
        Assert.Equal("utf-8", process.StartInfo.StandardOutputEncoding!.WebName);
        Assert.Equal("utf-8", process.StartInfo.StandardErrorEncoding!.WebName);

        if (!process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }
}
