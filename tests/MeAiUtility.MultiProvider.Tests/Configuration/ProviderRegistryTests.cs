using MeAiUtility.MultiProvider.Configuration;

namespace MeAiUtility.MultiProvider.Tests.Configuration;

public class ProviderRegistryTests
{
    [Test]
    public void RegisterAndResolve_Works()
    {
        var registry = new ProviderRegistry();
        registry.Register("OpenAI", typeof(string));
        Assert.That(registry.Resolve("OpenAI"), Is.EqualTo(typeof(string)));
    }
}
