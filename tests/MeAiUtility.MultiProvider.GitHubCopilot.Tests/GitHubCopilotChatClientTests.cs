using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotChatClientTests
{
    [Test]
    public async Task GetResponseAsync_ConvertsSessionConfig()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions { ModelId = "gpt-5", ReasoningEffort = ReasoningEffortLevel.High };
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Text, Is.EqualTo("ok"));
    }

    [Test]
    public void GetResponseAsync_DoesNotDoubleWrap_MultiProviderException()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        var originalException = new CopilotRuntimeException("inner", "GitHubCopilot", null, null, "trace123");
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(originalException);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.That(ex, Is.SameAs(originalException), "MultiProviderException は二重ラップされずにそのまま再スローされること");
    }

    [Test]
    public void GetResponseAsync_ThrowsInvalidRequest_WhenModelIdIsNotInCatalog()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5-mini", false), new CopilotModelInfo("gpt-5", true)]);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions { ModelId = "GPT-5 mini" };

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex!.Message, Does.Contain("Unknown GitHub Copilot model id 'GPT-5 mini'"));
        Assert.That(ex.Message, Does.Contain("gpt-5-mini"));
    }

    [Test]
    public async Task GetService_ReturnsCopilotModelCatalog()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5-mini", false)]);

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var catalog = sut.GetService(typeof(ICopilotModelCatalog)) as ICopilotModelCatalog;
        var models = await catalog!.ListModelsAsync();

        Assert.That(catalog, Is.Not.Null);
        Assert.That(models.Select(static x => x.ModelId), Is.EqualTo(new[] { "gpt-5-mini" }));
    }

    [Test]
    public void GetResponseAsync_RejectsResponseFormat()
    {
        var wrapper = CreateSuccessfulWrapper();
        var sut = CreateSut(wrapper);
        var options = new ChatOptions();
        options.ResponseFormat = ChatResponseFormat.Json;

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex!.FeatureName, Is.EqualTo("ResponseFormat"));
    }

    [Test]
    [Property("IntegrationPointId", "T-4-02")]
    public void GetResponseAsync_WrapsSendFailureWithSendOperation()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("send failed"));

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.Send));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-C-03")]
    public async Task GetResponseAsync_PropagatesAttachmentsSkillDirectoriesDisabledSkillsAndTimeout()
    {
        CopilotSessionConfig? captured = null;
        var skillDirectory = GetAbsoluteTestPath("skills");
        var dataPath = GetAbsoluteTestPath("data.json");
        var morePath = GetAbsoluteTestPath("more.txt");
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            TimeoutSeconds = 300,
            SkillDirectories = [skillDirectory],
            DisabledSkills = ["skill-a", "skill-b"],
            Attachments =
            [
                new FileAttachment { Path = dataPath, DisplayName = "data" },
                new FileAttachment { Path = morePath },
            ],
        };

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(300));
        Assert.That(captured.SkillDirectories, Is.EqualTo(new[] { skillDirectory }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "skill-a", "skill-b" }));
        Assert.That(captured.Attachments, Has.Count.EqualTo(2));
        Assert.That(captured.Attachments![0].Path, Is.EqualTo(dataPath));
        Assert.That(captured.Attachments[0].DisplayName, Is.EqualTo("data"));
        Assert.That(captured.Attachments[1].Path, Is.EqualTo(morePath));
    }

    [Test]
    [Property("IntegrationPointId", "T-3-03")]
    public async Task GetResponseAsync_UsesAdvancedOptionsFallback_WhenTypedSkillPropertiesAreNotSpecified()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ext = new ExtensionParameters();
        ext.Set("copilot.skillDirectories", new[] { @"D:\from-advanced" });
        ext.Set("copilot.disabledSkills", new[] { "skill-from-advanced" });
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\from-advanced" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "skill-from-advanced" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-3-05")]
    public async Task GetResponseAsync_PrefersTypedSkillPropertiesOverAdvancedOptions()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ext = new ExtensionParameters();
        ext.Set("copilot.skillDirectories", new[] { @"D:\from-advanced" });
        ext.Set("copilot.disabledSkills", new[] { "skill-from-advanced" });
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            SkillDirectories = [@"D:\typed"],
            DisabledSkills = ["typed-skill"],
        };

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\typed" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "typed-skill" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-10")]
    [Property("IntegrationPointId", "T-3-04")]
    public async Task GetResponseAsync_AllowsTypedSkillDirectoriesWithAdvancedOptionDisabledSkills()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var ext = new ExtensionParameters();
        ext.Set("copilot.disabledSkills", new[] { "advanced-disabled" });
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            SkillDirectories = [@"D:\typed-skill-dir"],
        };

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\typed-skill-dir" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "advanced-disabled" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-2-05")]
    [Property("IntegrationPointId", "T-3-09")]
    public async Task GetResponseAsync_AcceptsEmptyAttachmentAndSkillCollections()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            Attachments = [],
            SkillDirectories = [],
            DisabledSkills = [],
        });

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Is.Empty);
        Assert.That(captured.SkillDirectories, Is.Empty);
        Assert.That(captured.DisabledSkills, Is.Empty);
    }

    [Test]
    [Property("IntegrationPointId", "T-3-10")]
    public async Task GetResponseAsync_PropagatesMultipleSkillDirectoriesInOrder()
    {
        CopilotSessionConfig? captured = null;
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured = config)
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            SkillDirectories = [@"D:\skills1", @"D:\skills2", @"D:\skills3"],
        });

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\skills1", @"D:\skills2", @"D:\skills3" }));
    }

    [TestCase(0, TestName = "T-5-03 TimeoutSeconds 0 is rejected")]
    [TestCase(-1, TestName = "T-5-04 TimeoutSeconds negative is rejected")]
    public void GetResponseAsync_RejectsInvalidTimeoutSeconds(int timeoutSeconds)
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            TimeoutSeconds = timeoutSeconds,
        };

        Assert.That(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options),
            Throws.InstanceOf<InvalidRequestException>().With.Message.Contains("TimeoutSeconds"));
    }

    [TestCase("", TestName = "T-2-09 Attachment empty path is rejected")]
    [TestCase(null, TestName = "T-2-10 Attachment null path is rejected")]
    [TestCase("relative-path.txt", TestName = "T-2-09 Attachment relative path is rejected")]
    public void GetResponseAsync_RejectsAttachmentWithInvalidPath(string? path)
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            Attachments =
            [
                new FileAttachment { Path = path! },
            ],
        };

        Assert.That(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options),
            Throws.InstanceOf<InvalidRequestException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-L-02")]
    [Property("IntegrationPointId", "T-L-03")]
    [Property("IntegrationPointId", "T-L-04")]
    public async Task GetResponseAsync_SequentialCalls_DoNotLeakRequestScopedOptions()
    {
        var captured = new List<CopilotSessionConfig>();
        var firstAttachmentPath = GetAbsoluteTestPath("one.json");
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => captured.Add(Clone(config)))
            .ReturnsAsync("ok");

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions { TimeoutSeconds = 120 }, new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions { TimeoutSeconds = 120 }, new NullLogger<GitHubCopilotChatClient>());

        await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "first")], CreateExecutionOptions(new ConversationExecutionOptions
        {
            Attachments = [new FileAttachment { Path = firstAttachmentPath }],
            SkillDirectories = [@"D:\skills-a"],
            TimeoutSeconds = 60,
        }));
        await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "second")], CreateExecutionOptions(new ConversationExecutionOptions
        {
            SkillDirectories = [@"D:\skills-b"],
            TimeoutSeconds = 300,
        }));
        await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "third")], new ChatOptions());

        Assert.That(captured, Has.Count.EqualTo(3));
        Assert.That(captured[0].Attachments, Has.Count.EqualTo(1));
        Assert.That(captured[0].SkillDirectories, Is.EqualTo(new[] { @"D:\skills-a" }));
        Assert.That(captured[0].TimeoutSeconds, Is.EqualTo(60));
        Assert.That(captured[1].Attachments, Is.Null);
        Assert.That(captured[1].SkillDirectories, Is.EqualTo(new[] { @"D:\skills-b" }));
        Assert.That(captured[1].TimeoutSeconds, Is.EqualTo(300));
        Assert.That(captured[2].Attachments, Is.Null);
        Assert.That(captured[2].SkillDirectories, Is.Null);
        Assert.That(captured[2].TimeoutSeconds, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-L-05")]
    public async Task GetResponseAsync_SequentialFailuresAndSuccess_ReportIndependentOperations()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.SetupSequence(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("list failed"))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)])
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.SetupSequence(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok")
            .ThrowsAsync(new InvalidOperationException("send failed"));

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var listModelsEx = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "first")]));
        var success = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "second")]);
        var sendEx = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "third")]));

        Assert.That(listModelsEx!.Operation, Is.EqualTo(CopilotOperation.ListModels));
        Assert.That(success.Text, Is.EqualTo("ok"));
        Assert.That(sendEx!.Operation, Is.EqualTo(CopilotOperation.Send));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-06")]
    [Property("IntegrationPointId", "T-C-09")]
    public void GetResponseAsync_WithAttachmentsAndTimeout_WrapsSendFailureAsSendOperation()
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("send failed"));

        var host = new CopilotClientHost(wrapper.Object, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());

        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            TimeoutSeconds = 1,
            Attachments = [new FileAttachment { Path = GetAbsoluteTestPath("payload.json") }],
        });

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.Send));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-2-01")]
    public async Task UT_IT_T_2_01__SingleAttachmentPropagatesPathAndDisplayName()
    {
        // 単一 attachment の Path と DisplayName がそのまま SDK wrapper に渡ることを確認する。
        CopilotSessionConfig? captured = null;
        var path = GetAbsoluteTestPath("data.json");
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments =
                [
                    new FileAttachment { Path = path, DisplayName = "data" },
                ],
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Has.Count.EqualTo(1));
        Assert.That(captured.Attachments![0].Path, Is.EqualTo(path));
        Assert.That(captured.Attachments[0].DisplayName, Is.EqualTo("data"));
    }

    [Test]
    [Property("IntegrationPointId", "T-2-02")]
    public async Task UT_IT_T_2_02__AttachmentWithNullDisplayNamePropagates()
    {
        // DisplayName が null のときも Path と null 値が保持されたまま渡ることを確認する。
        CopilotSessionConfig? captured = null;
        var path = GetAbsoluteTestPath("file.txt");
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments =
                [
                    new FileAttachment { Path = path, DisplayName = null },
                ],
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Has.Count.EqualTo(1));
        Assert.That(captured.Attachments![0].Path, Is.EqualTo(path));
        Assert.That(captured.Attachments[0].DisplayName, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-2-03")]
    public async Task UT_IT_T_2_03__ThreeAttachmentsPropagateInOrder()
    {
        // 複数 attachment の順序が保持されたまま渡ることを確認する。
        CopilotSessionConfig? captured = null;
        var firstPath = GetAbsoluteTestPath("one.json");
        var secondPath = GetAbsoluteTestPath("two.json");
        var thirdPath = GetAbsoluteTestPath("three.json");
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments =
                [
                    new FileAttachment { Path = firstPath, DisplayName = "one" },
                    new FileAttachment { Path = secondPath, DisplayName = "two" },
                    new FileAttachment { Path = thirdPath, DisplayName = "three" },
                ],
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments!.Select(static attachment => attachment.Path), Is.EqualTo(new[] { firstPath, secondPath, thirdPath }));
        Assert.That(captured.Attachments!.Select(static attachment => attachment.DisplayName), Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-2-04")]
    public async Task UT_IT_T_2_04__NullAttachmentsCompletesNormally()
    {
        // Attachments=null を明示しても正常終了することを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = null,
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-2-05")]
    public async Task UT_IT_T_2_05__EmptyAttachmentsListCompletesNormally()
    {
        // 空の attachment 一覧でも例外にならず空配列として渡ることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = [],
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Is.Empty);
    }

    [Test]
    [Property("IntegrationPointId", "T-2-10")]
    public void UT_IT_T_2_10__NullAttachmentPathIsRejected()
    {
        // Attachment.Path が null の場合は path を含む InvalidRequestException で拒否する。
        var wrapper = CreateSuccessfulWrapper();
        var sut = CreateSut(wrapper);

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")],
                CreateExecutionOptions(new ConversationExecutionOptions
                {
                    Attachments =
                    [
                        new FileAttachment { Path = null!, DisplayName = "data" },
                    ],
                })));

        Assert.That(ex!.Message, Does.Contain("path").IgnoreCase);
    }

    [Test]
    [Property("IntegrationPointId", "T-3-06")]
    public async Task UT_IT_T_3_06__TypedDisabledSkillsPreferredOverAdvancedOptions()
    {
        // typed DisabledSkills が AdvancedOptions より優先されることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);
        var extensions = new ExtensionParameters();
        extensions.Set("copilot.disabledSkills", new[] { "advanced-skill" });
        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            DisabledSkills = ["typed-skill"],
        });
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = extensions;

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.DisabledSkills, Is.EqualTo(new[] { "typed-skill" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-3-07")]
    public async Task UT_IT_T_3_07__AdvancedOptionsOnlyStillWorksWhenTypedPropertiesAreNull()
    {
        // typed 値が null の場合のみ AdvancedOptions の skill 設定が採用されることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);
        var extensions = new ExtensionParameters();
        extensions.Set("copilot.skillDirectories", new[] { @"D:\adv-skills" });
        extensions.Set("copilot.disabledSkills", new[] { "advanced-disabled" });
        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            SkillDirectories = null,
            DisabledSkills = null,
        });
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = extensions;

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\adv-skills" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "advanced-disabled" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-3-08")]
    public async Task UT_IT_T_3_08__AllPropertiesNullCompletesNormally()
    {
        // typed / AdvancedOptions とも未指定のままでも正常終了することを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions()));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.Null);
        Assert.That(captured.DisabledSkills, Is.Null);
        Assert.That(captured.Attachments, Is.Null);
        Assert.That(captured.TimeoutSeconds, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-3-09")]
    public async Task UT_IT_T_3_09__EmptySkillDirectoriesListCompletesNormally()
    {
        // 空の SkillDirectories を許容することを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                SkillDirectories = [],
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.Empty);
    }

    [Test]
    [Property("IntegrationPointId", "T-3-10")]
    public async Task UT_IT_T_3_10__MultipleSkillDirectoriesPropagateInOrder()
    {
        // 複数 SkillDirectories の順序が維持されることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                SkillDirectories = [@"D:\s1", @"D:\s2", @"D:\s3"],
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\s1", @"D:\s2", @"D:\s3" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-4-07")]
    public void UT_IT_T_4_07__InnerExceptionIsPreservedAndOperationIsListModels()
    {
        // ListModels 失敗時に InnerException と Operation=ListModels が維持されることを確認する。
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("list-fail"));
        var sut = CreateSut(wrapper);

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.ListModels));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(ex.InnerException!.Message, Is.EqualTo("list-fail"));
    }

    [Test]
    [Property("IntegrationPointId", "T-5-04")]
    public void UT_IT_T_5_04__NegativeTimeoutSecondsIsRejected()
    {
        // 負の TimeoutSeconds は InvalidRequestException で拒否する。
        var sut = CreateSut(CreateSuccessfulWrapper());

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")],
                CreateExecutionOptions(new ConversationExecutionOptions
                {
                    TimeoutSeconds = -1,
                })));

        Assert.That(ex!.Message, Does.Contain("TimeoutSeconds"));
    }

    [Test]
    [Property("IntegrationPointId", "T-5-05")]
    public async Task UT_IT_T_5_05__MinimalPositiveTimeoutSecondsIsAccepted()
    {
        // 最小の正値 1 がそのまま送信設定に反映されることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                TimeoutSeconds = 1,
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(1));
    }

    [Test]
    [Property("IntegrationPointId", "T-5-06")]
    public async Task UT_IT_T_5_06__VeryLargeTimeoutSecondsIsAccepted()
    {
        // 非常に大きい TimeoutSeconds でもそのまま反映されることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                TimeoutSeconds = int.MaxValue,
            }));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(int.MaxValue));
    }

    [Test]
    [Property("IntegrationPointId", "T-5-07")]
    public async Task UT_IT_T_5_07__NullTimeoutFallsBackToProviderDefault()
    {
        // request 側の TimeoutSeconds 未指定時に provider default が SDK invocation に使われることを確認する。
        CopilotSdkInvocation? captured = null;
        var providerOptions = new GitHubCopilotProviderOptions { TimeoutSeconds = 120 };
        var wrapper = new GitHubCopilotSdkWrapper(
            providerOptions,
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: _ => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]),
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });
        var host = new CopilotClientHost(wrapper, providerOptions, new NullLogger<CopilotClientHost>());
        var sut = new GitHubCopilotChatClient(host, providerOptions, new NullLogger<GitHubCopilotChatClient>());

        var response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions()));

        Assert.That(response.Text, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(120));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-01")]
    public async Task UT_IT_T_C_01__AttachmentsAndSkillDirectoriesBothPropagate()
    {
        // Attachments と SkillDirectories を同時指定したときの伝播を確認する。
        CopilotSessionConfig? captured = null;
        var attachmentPath = GetAbsoluteTestPath("f.txt");
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = [new FileAttachment { Path = attachmentPath, DisplayName = "payload" }],
                SkillDirectories = [@"D:\sk"],
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Has.Count.EqualTo(1));
        Assert.That(captured.SkillDirectories, Is.EqualTo(new[] { @"D:\sk" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-02")]
    public async Task UT_IT_T_C_02__AttachmentsAndDisabledSkillsBothPropagate()
    {
        // Attachments と DisabledSkills を同時指定したときの伝播を確認する。
        CopilotSessionConfig? captured = null;
        var attachmentPath = GetAbsoluteTestPath("f.txt");
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = [new FileAttachment { Path = attachmentPath }],
                DisabledSkills = ["s1"],
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Attachments, Has.Count.EqualTo(1));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "s1" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-05")]
    public async Task UT_IT_T_C_05__SkillDirectoriesAndTimeoutBothPropagate()
    {
        // SkillDirectories と TimeoutSeconds を同時指定したときの伝播を確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);

        _ = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                SkillDirectories = [@"D:\sk"],
                TimeoutSeconds = 60,
            }));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\sk" }));
        Assert.That(captured.TimeoutSeconds, Is.EqualTo(60));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-06")]
    public void UT_IT_T_C_06__TimeoutSpecifiedAndSendFailureHasSendOperation()
    {
        // Timeout 指定付き送信失敗が Send operation でラップされることを確認する。
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("timeout-sim"));
        var sut = CreateSut(wrapper);

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")],
                CreateExecutionOptions(new ConversationExecutionOptions
                {
                    TimeoutSeconds = 1,
                })));

        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.Send));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-C-09")]
    public void UT_IT_T_C_09__AttachmentsWithSendFailureHasSendOperation()
    {
        // Attachment 付き送信失敗が Send operation でラップされることを確認する。
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("send-fail"));
        var sut = CreateSut(wrapper);

        var ex = Assert.ThrowsAsync<CopilotRuntimeException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "hi")],
                CreateExecutionOptions(new ConversationExecutionOptions
                {
                    Attachments = [new FileAttachment { Path = GetAbsoluteTestPath("f.txt") }],
                })));

        Assert.That(ex!.Operation, Is.EqualTo(CopilotOperation.Send));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    [Property("IntegrationPointId", "T-C-10")]
    public async Task UT_IT_T_C_10__TypedSkillDirectoriesWithAdvancedOptionsDisabledSkillsMix()
    {
        // typed SkillDirectories と AdvancedOptions DisabledSkills を混在指定できることを確認する。
        CopilotSessionConfig? captured = null;
        var wrapper = CreateSuccessfulWrapper(config => captured = config);
        var sut = CreateSut(wrapper);
        var extensions = new ExtensionParameters();
        extensions.Set("copilot.disabledSkills", new[] { "advanced-disabled" });
        var options = CreateExecutionOptions(new ConversationExecutionOptions
        {
            SkillDirectories = [@"D:\typed"],
        });
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = extensions;

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\typed" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "advanced-disabled" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-L-02")]
    public async Task UT_IT_T_L_02__AttachmentsDoNotLeakAcrossConsecutiveCalls()
    {
        // 連続呼び出しで attachment が次の request に漏れないことを確認する。
        var captured = new List<CopilotSessionConfig>();
        var firstAttachmentPath = GetAbsoluteTestPath("a.txt");
        var secondAttachmentPath = GetAbsoluteTestPath("b.txt");
        var wrapper = CreateSuccessfulWrapper(config => captured.Add(Clone(config)));
        var sut = CreateSut(wrapper, new GitHubCopilotProviderOptions { TimeoutSeconds = 120 });

        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "first")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = [new FileAttachment { Path = firstAttachmentPath }],
            }));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "second")],
            CreateExecutionOptions(new ConversationExecutionOptions()));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "third")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                Attachments = [new FileAttachment { Path = secondAttachmentPath }],
            }));

        Assert.That(captured, Has.Count.EqualTo(3));
        Assert.That(captured[0].Attachments, Has.Count.EqualTo(1));
        Assert.That(captured[0].Attachments![0].Path, Is.EqualTo(firstAttachmentPath));
        Assert.That(captured[1].Attachments, Is.Null);
        Assert.That(captured[2].Attachments, Has.Count.EqualTo(1));
        Assert.That(captured[2].Attachments![0].Path, Is.EqualTo(secondAttachmentPath));
    }

    [Test]
    [Property("IntegrationPointId", "T-L-03")]
    public async Task UT_IT_T_L_03__SkillDirectoriesDoNotLeakAcrossConsecutiveCalls()
    {
        // 連続呼び出しで SkillDirectories が次の request に漏れないことを確認する。
        var captured = new List<CopilotSessionConfig>();
        var wrapper = CreateSuccessfulWrapper(config => captured.Add(Clone(config)));
        var sut = CreateSut(wrapper);

        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "first")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                SkillDirectories = [@"D:\A"],
            }));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "second")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                SkillDirectories = [@"D:\B"],
            }));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "third")],
            CreateExecutionOptions(new ConversationExecutionOptions()));

        Assert.That(captured, Has.Count.EqualTo(3));
        Assert.That(captured[0].SkillDirectories, Is.EqualTo(new[] { @"D:\A" }));
        Assert.That(captured[1].SkillDirectories, Is.EqualTo(new[] { @"D:\B" }));
        Assert.That(captured[2].SkillDirectories, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-L-04")]
    public async Task UT_IT_T_L_04__TimeoutSecondsDoNotLeakAcrossConsecutiveCalls()
    {
        // 連続呼び出しで TimeoutSeconds が次の request に漏れないことを確認する。
        var captured = new List<CopilotSessionConfig>();
        var wrapper = CreateSuccessfulWrapper(config => captured.Add(Clone(config)));
        var sut = CreateSut(wrapper, new GitHubCopilotProviderOptions { TimeoutSeconds = 120 });

        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "first")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                TimeoutSeconds = 60,
            }));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "second")],
            CreateExecutionOptions(new ConversationExecutionOptions
            {
                TimeoutSeconds = 300,
            }));
        await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "third")],
            CreateExecutionOptions(new ConversationExecutionOptions()));

        Assert.That(captured, Has.Count.EqualTo(3));
        Assert.That(captured[0].TimeoutSeconds, Is.EqualTo(60));
        Assert.That(captured[1].TimeoutSeconds, Is.EqualTo(300));
        Assert.That(captured[2].TimeoutSeconds, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-L-05")]
    public async Task UT_IT_T_L_05__PhaseIsIndependentPerCall()
    {
        // 各呼び出しで phase 情報が独立して決定されることを確認する。
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.SetupSequence(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("list-fail"))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)])
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.SetupSequence(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok")
            .ThrowsAsync(new InvalidOperationException("send-fail"));
        var sut = CreateSut(wrapper);

        var first = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "first")]));
        var second = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "second")]);
        var third = Assert.ThrowsAsync<CopilotRuntimeException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "third")]));

        Assert.That(first!.Operation, Is.EqualTo(CopilotOperation.ListModels));
        Assert.That(second.Text, Is.EqualTo("ok"));
        Assert.That(third!.Operation, Is.EqualTo(CopilotOperation.Send));
    }

    private static GitHubCopilotChatClient CreateSut(Mock<ICopilotSdkWrapper> wrapper, GitHubCopilotProviderOptions? providerOptions = null)
    {
        var actualOptions = providerOptions ?? new GitHubCopilotProviderOptions();
        var host = new CopilotClientHost(wrapper.Object, actualOptions, new NullLogger<CopilotClientHost>());
        return new GitHubCopilotChatClient(host, actualOptions, new NullLogger<GitHubCopilotChatClient>());
    }

    private static Mock<ICopilotSdkWrapper> CreateSuccessfulWrapper(Action<CopilotSessionConfig>? capture = null, string response = "ok")
    {
        var wrapper = new Mock<ICopilotSdkWrapper>();
        wrapper.Setup(x => x.ListModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CopilotModelInfo("gpt-5", true)]);
        wrapper.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<CopilotSessionConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, CopilotSessionConfig, CancellationToken>((_, config, _) => capture?.Invoke(Clone(config)))
            .ReturnsAsync(response);
        return wrapper;
    }

    private static ChatOptions CreateExecutionOptions(ConversationExecutionOptions execution)
    {
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = execution;
        return options;
    }

    private static string GetAbsoluteTestPath(string fileName)
        => Path.Combine(Path.GetTempPath(), "meai-ghcp-tests", fileName);

    private static CopilotSessionConfig Clone(CopilotSessionConfig config)
    {
        FileAttachment[]? attachments = null;
        if (config.Attachments is not null)
        {
            attachments = config.Attachments.Select(static attachment => new FileAttachment
            {
                Path = attachment.Path,
                DisplayName = attachment.DisplayName,
            }).ToArray();
        }

        return new CopilotSessionConfig
        {
            ModelId = config.ModelId,
            ReasoningEffort = config.ReasoningEffort,
            Streaming = config.Streaming,
            TimeoutSeconds = config.TimeoutSeconds,
            ProviderOverride = config.ProviderOverride,
            Attachments = attachments,
            SkillDirectories = config.SkillDirectories?.ToArray(),
            DisabledSkills = config.DisabledSkills?.ToArray(),
        };
    }
}


