using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class CopilotClientHostTests
{
    [Test]
    [Property("IntegrationPointId", "T-4-01")]
    public async Task ListModelsAsync_ThrowsRuntimeExceptionOnFailure()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions { CliPath = "copilot" }, new NullLogger<CopilotClientHost>());

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.CopilotRuntimeException>(async () => await host.ListModelsAsync());
        Assert.That(ex!.Operation, Is.EqualTo(MeAiUtility.MultiProvider.Options.CopilotOperation.ListModels));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }
}
