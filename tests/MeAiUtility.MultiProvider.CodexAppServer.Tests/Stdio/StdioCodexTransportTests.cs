using MeAiUtility.MultiProvider.CodexAppServer.Options;
using System.Diagnostics;
using MeAiUtility.MultiProvider.CodexAppServer.Stdio;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.Stdio;

public class StdioCodexTransportTests
{
    [Test]
    public void BuildEffectiveArguments_AddsAppServer_WhenArgumentsAreEmpty()
    {
        var arguments = StdioCodexTransport.BuildEffectiveArguments("codex", []);

        Assert.That(arguments, Is.EqualTo(new[] { "app-server" }));
    }

    [Test]
    public void BuildEffectiveArguments_Throws_WhenAppServerIsSpecifiedAsOnlyArgumentForCodexCommand()
    {
        var ex = Assert.Throws<InvalidRequestException>(() => StdioCodexTransport.BuildEffectiveArguments("codex", ["app-server"]));

        Assert.That(ex!.Message, Does.Contain("do not pass it explicitly"));
    }

    [Test]
    public void BuildEffectiveArguments_Throws_WhenAppServerIsDuplicated()
    {
        var ex = Assert.Throws<InvalidRequestException>(() => StdioCodexTransport.BuildEffectiveArguments("codex", ["app-server", "app-server"]));

        Assert.That(ex!.Message, Does.Contain("duplicate 'app-server'"));
    }

    [Test]
    public void BuildEffectiveArguments_AllowsSingleAppServerForNonCodexCommand()
    {
        var arguments = StdioCodexTransport.BuildEffectiveArguments("custom-wrapper", ["app-server"]);

        Assert.That(arguments, Is.EqualTo(new[] { "app-server" }));
    }

    [Test]
    public void CodexProcessExitedException_IncludesDiagnosticsInMessage()
    {
        var ex = new CodexProcessExitedException("codex.cmd", ["app-server", "--listen", "stdio://"], 3, "fatal");

        Assert.That(ex.Message, Does.Contain("Command='codex.cmd'"));
        Assert.That(ex.Message, Does.Contain("Arguments='app-server --listen stdio://'"));
        Assert.That(ex.Message, Does.Contain("ExitCode=3"));
        Assert.That(ex.Message, Does.Contain("StderrTail='fatal'"));
    }

    [Test]
    public async Task StartAsync_ResetsStderrTail_BeforeRestart()
    {
        var runner = new SequencedShellProcessRunner("first-stderr", "second-stderr");
        var transport = new StdioCodexTransport(
            runner,
            NullLogger<StdioCodexTransport>.Instance,
            new CodexAppServerProviderOptions { CodexCommand = "custom-wrapper" },
            null);

        await transport.StartAsync();
        await transport.DisposeAsync();

        Assert.That(transport.StderrTailForDiagnostics?.TrimEnd(), Is.EqualTo("first-stderr"));

        await transport.StartAsync();
        await transport.DisposeAsync();

        Assert.That(transport.StderrTailForDiagnostics?.TrimEnd(), Is.EqualTo("second-stderr"));
    }

    private sealed class SequencedShellProcessRunner : ICodexProcessRunner
    {
        private readonly Queue<string> _stderrMessages;

        public SequencedShellProcessRunner(params string[] stderrMessages)
        {
            _stderrMessages = new Queue<string>(stderrMessages);
        }

        public Task<Process> StartAsync(CodexProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            _ = startInfo;
            cancellationToken.ThrowIfCancellationRequested();

            if (_stderrMessages.Count == 0)
            {
                throw new InvalidOperationException("No more scripted processes are available.");
            }

            var stderrMessage = _stderrMessages.Dequeue();
            var process = StartShellProcess(stderrMessage);
            return Task.FromResult(process);
        }

        private static Process StartShellProcess(string stderrMessage)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/d /s /c \"echo {stderrMessage} 1>&2\"";
            }
            else
            {
                startInfo.FileName = "/bin/sh";
                startInfo.Arguments = $"-c \"printf '%s\\n' '{stderrMessage}' >&2\"";
            }

            var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Failed to start scripted process.");
            }

            return process;
        }
    }
}
