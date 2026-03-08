using MeAiUtility.MultiProvider.Samples;

namespace MeAiUtility.MultiProvider.IntegrationTests.Samples;

public class SampleSmokeTests
{
    [Test]
    public async Task BasicSamples_RunSuccessfully()
    {
        await Task.CompletedTask;
        var azureOptions = ExtensionParametersSample.CreateAzureOptions();
        var copilotOptions = ExtensionParametersSample.CreateCopilotOptions();

        Assert.That(azureOptions.AdditionalProperties.ContainsKey("meai.extensions"), Is.True);
        Assert.That(copilotOptions.AdditionalProperties.ContainsKey("meai.extensions"), Is.True);
    }
}
