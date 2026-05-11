using MeAiUtility.MultiProvider.CodexAppServer.Stdio;
using MeAiUtility.MultiProvider.Exceptions;

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
}
