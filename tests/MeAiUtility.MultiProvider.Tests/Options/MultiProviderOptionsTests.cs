using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.Tests.Options;

public class MultiProviderOptionsTests
{
    [Test]
    public void Validate_RequiresMatchingProviderSection()
    {
        var options = new MultiProviderOptions { Provider = "OpenAI", OpenAI = new object() };
        Assert.That(() => options.Validate(), Throws.Nothing);
    }

    [Test]
    public void Validate_ThrowsForUnknownProvider()
    {
        var options = new MultiProviderOptions { Provider = "Unknown" };
        Assert.That(() => options.Validate(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Validate_AcceptsCodexAppServerProvider()
    {
        var options = new MultiProviderOptions { Provider = "CodexAppServer", CodexAppServer = new object() };
        Assert.That(() => options.Validate(), Throws.Nothing);
    }
}
