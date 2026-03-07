using MeAiUtility.MultiProvider.Exceptions;

namespace MeAiUtility.MultiProvider.Tests.Exceptions;

public class MultiProviderExceptionTests
{
    [Test]
    public void ProviderException_StoresHttpData()
    {
        var ex = new ProviderException("err", "OpenAI", "trace", 500, "body");
        Assert.That(ex.ProviderName, Is.EqualTo("OpenAI"));
        Assert.That(ex.TraceId, Is.EqualTo("trace"));
        Assert.That(ex.StatusCode, Is.EqualTo(500));
        Assert.That(ex.ResponseBody, Is.EqualTo("body"));
    }

    [Test]
    public void TimeoutException_StoresTimeoutSeconds()
    {
        var ex = new MeAiUtility.MultiProvider.Exceptions.TimeoutException("timeout", "OpenAI", 12, "trace");
        Assert.That(ex.TimeoutSeconds, Is.EqualTo(12));
    }

    [Test]
    public void CopilotRuntimeException_StoresCliContext()
    {
        var ex = new CopilotRuntimeException("runtime", "GitHubCopilot", "copilot", 1, "trace");
        Assert.That(ex.CliPath, Is.EqualTo("copilot"));
        Assert.That(ex.ExitCode, Is.EqualTo(1));
    }
}
