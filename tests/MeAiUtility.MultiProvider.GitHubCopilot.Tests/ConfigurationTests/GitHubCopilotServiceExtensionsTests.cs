using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Configuration;
using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests;

public class GitHubCopilotServiceExtensionsTests
{
    [Test]
    [Property("IntegrationPointId", "T-1-01")]
    public void AddGitHubCopilot_RegistersSdkWrapperAndChatClient()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilot(configuration.Object);

        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();
        var chatClient = provider.GetRequiredService<GitHubCopilotChatClient>();
        var catalog = provider.GetRequiredService<ICopilotModelCatalog>();

        Assert.That(wrapper, Is.TypeOf<GitHubCopilotSdkWrapper>());
        Assert.That(chatClient, Is.Not.Null);
        Assert.That(catalog, Is.TypeOf<GitHubCopilotChatClient>());
    }

    [Test]
    [Property("IntegrationPointId", "T-1-02")]
    public void UT_IT_T_1_02__AddGitHubCopilotAndExplicitIChatClientResolvesGitHubCopilotChatClient()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilot(configuration.Object);
        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<GitHubCopilotChatClient>());

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();

        Assert.That(chatClient, Is.TypeOf<GitHubCopilotChatClient>());
    }

    [Test]
    [Property("IntegrationPointId", "T-1-09")]
    public void AddGitHubCopilot_ThrowsOnNullConfiguration()
    {
        var services = new ServiceCollection();
        Assert.That(() => services.AddGitHubCopilot(null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void AddGitHubCopilotSdkWrapper_OverridesDefaultWrapper()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MeAiUtility.MultiProvider.GitHubCopilot.Options.GitHubCopilotProviderOptions());
        services.AddSingleton<ICopilotSdkWrapper, StubCopilotSdkWrapper>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotSdkWrapper();

        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();

        Assert.That(wrapper, Is.TypeOf<GitHubCopilotSdkWrapper>());
        Assert.That(provider.GetRequiredService<GitHubCopilotSdkWrapper>(), Is.SameAs(wrapper));
    }

    [Test]
    public void AddGitHubCopilotCliSdkWrapper_RegistersCompatibilityType()
    {
        // 後方互換 API の登録動作を確認するテストのため、obsolete 警告を抑止する。
#pragma warning disable CS0618
        var services = new ServiceCollection();
        services.AddSingleton(new MeAiUtility.MultiProvider.GitHubCopilot.Options.GitHubCopilotProviderOptions());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotCliSdkWrapper();

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<ICopilotSdkWrapper>(), Is.TypeOf<GitHubCopilotSdkWrapper>());
        Assert.That(provider.GetRequiredService<GitHubCopilotCliSdkWrapper>(), Is.Not.Null);
#pragma warning restore CS0618
    }

    [Test]
    [Property("IntegrationPointId", "T-1-03")]
    public void AddGitHubCopilotProvider_RegistersModelCatalog()
    {
        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<ICopilotModelCatalog>(), Is.TypeOf<GitHubCopilotChatClient>());
    }

    [Test]
    [Property("IntegrationPointId", "T-1-04")]
    public void AddGitHubCopilotProvider_DefaultWrapperFailsFastOnListModels()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.ListModelsAsync());
        Assert.That(ex!.Message, Does.Contain("AddGitHubCopilotSdkWrapper()"));
        Assert.That(ex.Message, Does.Contain("AddGitHubCopilot()"));
    }

    [Test]
    [Property("IntegrationPointId", "T-1-05")]
    public void AddGitHubCopilotProvider_DefaultWrapperFailsFastOnSend()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.SendAsync("hello", new CopilotSessionConfig()));
        Assert.That(ex!.Message, Does.Contain("AddGitHubCopilotSdkWrapper()"));
        Assert.That(ex.Message, Does.Contain("AddGitHubCopilot()"));
    }

    [Test]
    [Property("IntegrationPointId", "T-1-06")]
    public void AddGitHubCopilotProvider_Only_ChatClientWrapsFailFastAsRuntimeException()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<GitHubCopilotChatClient>());
        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.CopilotRuntimeException>(
            async () => await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.ListModels));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-1-07")]
    public void UT_IT_T_1_07__TwoStepRegistrationWorksCorrectly()
    {
        // AddGitHubCopilotProvider + AddGitHubCopilotSdkWrapper の 2 段階登録で
        // ICopilotSdkWrapper が GitHubCopilotSdkWrapper 型として解決されることを確認する。
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        services.AddGitHubCopilotSdkWrapper();

        using var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<ICopilotSdkWrapper>();

        Assert.That(wrapper, Is.TypeOf<GitHubCopilotSdkWrapper>());
        // DefaultCopilotSdkWrapper (fail-fast スタブ) ではなく本物の wrapper が返ること
        Assert.That(provider.GetRequiredService<GitHubCopilotSdkWrapper>(), Is.SameAs(wrapper));
    }

    [Test]
    [Property("IntegrationPointId", "T-1-08")]
    public async Task UT_IT_T_1_08__CustomSdkWrapperIsUsedWhenRegisteredAfterProvider()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        services.AddSingleton<ICopilotSdkWrapper, StubCopilotSdkWrapper>();

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<GitHubCopilotChatClient>();

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.That(response.Text, Is.EqualTo("stub"));
        Assert.That(provider.GetRequiredService<ICopilotSdkWrapper>(), Is.TypeOf<StubCopilotSdkWrapper>());
    }

    [Test]
    [Property("IntegrationPointId", "T-L-01")]
    public void UT_IT_T_L_01__GitHubCopilotChatClientIsSingleton()
    {
        // AddGitHubCopilot 登録後に GitHubCopilotChatClient が singleton として解決されることを確認する。
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilot(configuration.Object);

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<GitHubCopilotChatClient>();
        var second = provider.GetRequiredService<GitHubCopilotChatClient>();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-08")]
    public async Task UT_IT_T_C_08__AddGitHubCopilotWithMockWrapperAndAttachmentsSkillDirectories()
    {
        var configuration = BuildConfiguration();
        var attachmentPath = Path.Combine(Path.GetTempPath(), "meai-ghcp-tests", "f.txt");
        var skillDirectory = Path.Combine(Path.GetTempPath(), "meai-ghcp-tests", "skills");
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGitHubCopilotProvider(configuration.Object);
        var captured = default(CopilotSessionConfig);
        var mockWrapper = new Mock<ICopilotSdkWrapper>();
        mockWrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        mockWrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");
        services.AddSingleton<ICopilotSdkWrapper>(mockWrapper.Object);
        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<GitHubCopilotChatClient>());

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            Attachments = [new FileAttachment { Path = attachmentPath, DisplayName = "payload" }],
            SkillDirectories = [skillDirectory],
        };

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Has.Count.EqualTo(1));
        Assert.That(captured.Attachments![0].Path, Is.EqualTo(attachmentPath));
        Assert.That(captured.Attachments[0].DisplayName, Is.EqualTo("payload"));
        Assert.That(captured.SkillDirectories, Is.EqualTo(new[] { skillDirectory }));
    }

    private static Mock<IConfiguration> BuildConfiguration()
    {
        var section = new Mock<IConfigurationSection>();
        section.SetupGet(x => x.Path).Returns("MultiProvider:GitHubCopilot");
        section.SetupGet(x => x.Key).Returns("GitHubCopilot");
        section.SetupGet(x => x.Value).Returns((string?)null);

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(x => x.GetSection("MultiProvider:GitHubCopilot")).Returns(section.Object);
        return configuration;
    }

    private sealed class StubCopilotSdkWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
            => Task.FromResult("stub");
    }
}


