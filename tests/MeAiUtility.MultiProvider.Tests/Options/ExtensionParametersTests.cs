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
}
