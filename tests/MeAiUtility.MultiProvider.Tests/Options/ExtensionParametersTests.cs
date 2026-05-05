using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.Tests.Options;

public class ExtensionParametersTests
{
    [Test]
    public void SetGetAndFilter_WorkCorrectly()
    {
        var ext = new ExtensionParameters();
        ext.Set("azure.data_sources", 1);
        Assert.That(ext.Get<int>("azure.data_sources"), Is.EqualTo(1));
        Assert.That(ext.Has("azure.data_sources"), Is.True);
        Assert.That(ext.GetAllForProvider("azure").Count, Is.EqualTo(1));
    }

    [Test]
    public void Set_AcceptsCodexPrefix()
    {
        var ext = new ExtensionParameters();
        ext.Set("codex.workingDirectory", @"D:\work");

        Assert.That(ext.Get<string>("codex.workingDirectory"), Is.EqualTo(@"D:\work"));
    }

    [Test]
    public void Set_AcceptsProviderPrefixCaseInsensitive()
    {
        var ext = new ExtensionParameters();
        ext.Set("Azure.data_sources", 1);

        Assert.That(ext.Get<int>("azure.data_sources"), Is.EqualTo(1));
        Assert.That(ext.GetAllForProvider("AZURE").Count, Is.EqualTo(1));
    }
}
