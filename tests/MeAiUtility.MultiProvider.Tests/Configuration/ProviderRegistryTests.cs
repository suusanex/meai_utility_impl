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

    [Test]
    public void TryResolve_ReturnsTrueWhenRegistered()
    {
        var registry = new ProviderRegistry();
        registry.Register("OpenAI", typeof(string));

        var result = registry.TryResolve("OpenAI", out var type);

        Assert.That(result, Is.True);
        Assert.That(type, Is.EqualTo(typeof(string)));
    }

    [Test]
    public void TryResolve_ReturnsFalseWhenNotRegistered()
    {
        var registry = new ProviderRegistry();

        var result = registry.TryResolve("Unknown", out var type);

        Assert.That(result, Is.False);
        Assert.That(type, Is.Null);
    }
}
