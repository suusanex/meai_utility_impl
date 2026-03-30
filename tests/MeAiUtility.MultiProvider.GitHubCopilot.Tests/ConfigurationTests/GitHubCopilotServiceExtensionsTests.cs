using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests;

public class GitHubCopilotServiceExtensionsTests
{
    [Test]
    public void AddGitHubCopilotCliSdkWrapper_OverridesDefaultWrapper()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MeAiUtility.MultiProvider.GitHubCopilot.Options.GitHubCopilotProviderOptions());
        services.AddSingleton<ICopilotSdkWrapper, StubCopilotSdkWrapper>();
        services.AddGitHubCopilotCliSdkWrapper();

        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();

        Assert.That(wrapper, Is.TypeOf<GitHubCopilotCliSdkWrapper>());
        Assert.That(provider.GetRequiredService<GitHubCopilotCliSdkWrapper>(), Is.SameAs(wrapper));
    }

    [Test]
    public void AddGitHubCopilotProvider_RegistersModelCatalog()
    {
        var section = new Mock<IConfigurationSection>();
        section.SetupGet(x => x.Path).Returns("MultiProvider:GitHubCopilot");
        section.SetupGet(x => x.Key).Returns("GitHubCopilot");
        section.SetupGet(x => x.Value).Returns((string?)null);

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(x => x.GetSection("MultiProvider:GitHubCopilot")).Returns(section.Object);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<ICopilotModelCatalog>(), Is.TypeOf<GitHubCopilotChatClient>());
    }

    private sealed class StubCopilotSdkWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("stub");
    }
}
