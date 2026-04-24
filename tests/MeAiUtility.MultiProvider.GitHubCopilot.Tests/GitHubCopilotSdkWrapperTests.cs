using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotSdkWrapperTests
{
    [Test]
    public async Task SendAsync_MapsOptionsToSdkInvocation()
    {
        CopilotSdkInvocation? captured = null;
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions
            {
                ModelId = "gpt-5-mini",
                Streaming = false,
                ConfigDir = @"D:\copilot-config",
                WorkingDirectory = @"D:\workspace",
                ClientName = "meai-tests",
                AvailableTools = ["read"],
                ExcludedTools = ["write"],
                InfiniteSessions = new InfiniteSessionOptions
                {
                    Enabled = true,
                    BackgroundCompactionThreshold = 0.8,
                    BufferExhaustionThreshold = 0.95,
                },
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });

        var config = new CopilotSessionConfig
        {
            ReasoningEffort = ReasoningEffortLevel.High,
            Streaming = true,
            ProviderOverride = new ProviderOverrideOptions
            {
                Type = "openai",
                BaseUrl = "https://api.openai.com/v1",
                ApiKey = "key",
            },
        };
        config.AdvancedOptions["copilot.mode"] = "plan";
        config.AdvancedOptions["copilot.configDir"] = @"D:\request-config";
        config.AdvancedOptions["copilot.workingDirectory"] = @"D:\request-work";
        config.AdvancedOptions["copilot.availableTools"] = new[] { "read", "write" };
        config.AdvancedOptions["copilot.excludedTools"] = new[] { "shell" };
        config.AdvancedOptions["copilot.agent"] = "custom-agent";
        config.AdvancedOptions["copilot.skillDirectories"] = new[] { @"D:\skills" };
        config.AdvancedOptions["copilot.disabledSkills"] = new[] { "lvt" };
        config.AdvancedOptions["copilot.mcp_servers"] = new Dictionary<string, object>
        {
            ["demo"] = new Dictionary<string, object> { ["type"] = "local" },
        };

        var response = await sut.SendAsync("prompt", config);

        Assert.That(response, Is.EqualTo("ok"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Prompt, Is.EqualTo("prompt"));
        Assert.That(captured.Mode, Is.EqualTo("plan"));
        Assert.That(captured.ModelId, Is.EqualTo("gpt-5-mini"));
        Assert.That(captured.ReasoningEffort, Is.EqualTo("high"));
        Assert.That(captured.Streaming, Is.True);
        Assert.That(captured.ConfigDir, Is.EqualTo(@"D:\request-config"));
        Assert.That(captured.WorkingDirectory, Is.EqualTo(@"D:\request-work"));
        Assert.That(captured.ClientName, Is.EqualTo("meai-tests"));
        Assert.That(captured.AvailableTools, Is.EqualTo(new[] { "read", "write" }));
        Assert.That(captured.ExcludedTools, Is.EqualTo(new[] { "shell" }));
        Assert.That(captured.ProviderOverride, Is.Not.Null);
        Assert.That(captured.ProviderOverride!.Type, Is.EqualTo("openai"));
        Assert.That(captured.Agent, Is.EqualTo("custom-agent"));
        Assert.That(captured.SkillDirectories, Is.EqualTo(new[] { @"D:\skills" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "lvt" }));
        Assert.That(captured.Attachments, Is.Null);
        Assert.That(captured.McpServers, Is.Not.Null);
        Assert.That(captured.McpServers!.ContainsKey("demo"), Is.True);
        Assert.That(captured.TimeoutSeconds, Is.GreaterThan(0));
    }

    [Test]
    public void SendAsync_RejectsUnsupportedAdvancedOption()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var config = new CopilotSessionConfig();
        config.AdvancedOptions["copilot.unknown"] = true;

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.SendAsync("prompt", config));

        Assert.That(ex!.Message, Does.Contain("copilot.unknown"));
    }

    [Test]
    public void SendAsync_RequiresToken_WhenLoggedInUserIsDisabled()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions
            {
                UseLoggedInUser = false,
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.SendAsync("prompt", new CopilotSessionConfig()));

        Assert.That(ex!.Message, Does.Contain("GitHubToken is required"));
    }

    [Test]
    public void ResolveClientAuthOptions_UsesTokenAuth_WhenTokenIsSpecified()
    {
        var options = new GitHubCopilotProviderOptions
        {
            GitHubToken = "ghu_test_token",
            UseLoggedInUser = true,
        };

        var authOptions = GitHubCopilotSdkWrapper.ResolveClientAuthOptions(options);

        Assert.That(authOptions.GitHubToken, Is.EqualTo("ghu_test_token"));
        Assert.That(authOptions.UseLoggedInUser, Is.False);
    }

    [Test]
    public void ResolveClientAuthOptions_UsesConfiguredLoggedInUser_WhenTokenIsNotSpecified()
    {
        var options = new GitHubCopilotProviderOptions
        {
            UseLoggedInUser = true,
        };

        var authOptions = GitHubCopilotSdkWrapper.ResolveClientAuthOptions(options);

        Assert.That(authOptions.GitHubToken, Is.Null);
        Assert.That(authOptions.UseLoggedInUser, Is.True);
    }

    [Test]
    public void SendAsync_RejectsInvalidStringListAdvancedOption_WithConsistentMessage()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var config = new CopilotSessionConfig();
        config.AdvancedOptions["copilot.availableTools"] = new { name = "read" };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.SendAsync("prompt", config));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Is.EqualTo("Advanced option 'copilot.availableTools' must be an array of strings."));
    }

    [Test]
    public void SendAsync_RejectsInvalidDictionaryAdvancedOption_WithConsistentMessage()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var config = new CopilotSessionConfig();
        config.AdvancedOptions["copilot.mcpServers"] = "invalid";

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.SendAsync("prompt", config));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Is.EqualTo("Advanced option 'copilot.mcpServers' must be an object."));
    }

    [Test]
    [Property("IntegrationPointId", "T-3-05")]
    public async Task SendAsync_UsesTypedSkillOptionsInPreferenceToAdvancedOptions()
    {
        CopilotSdkInvocation? captured = null;
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });

        var config = new CopilotSessionConfig
        {
            SkillDirectories = [@"D:\typed-skills"],
            DisabledSkills = ["typed-disabled"],
        };
        config.AdvancedOptions["copilot.skillDirectories"] = new[] { @"D:\advanced-skills" };
        config.AdvancedOptions["copilot.disabledSkills"] = new[] { "advanced-disabled" };

        _ = await sut.SendAsync("prompt", config);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.SkillDirectories, Is.EqualTo(new[] { @"D:\typed-skills" }));
        Assert.That(captured.DisabledSkills, Is.EqualTo(new[] { "typed-disabled" }));
    }

    [Test]
    [Property("IntegrationPointId", "T-C-04")]
    public async Task SendAsync_MapsAttachmentsAndRequestTimeoutOverride()
    {
        CopilotSdkInvocation? captured = null;
        var firstAttachmentPath = GetAbsoluteTestPath("data.json");
        var secondAttachmentPath = GetAbsoluteTestPath("report.txt");
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions
            {
                TimeoutSeconds = 120,
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });

        var config = new CopilotSessionConfig
        {
            TimeoutSeconds = 300,
            Attachments =
            [
                new FileAttachment { Path = firstAttachmentPath, DisplayName = "data" },
                new FileAttachment { Path = secondAttachmentPath },
            ],
        };

        _ = await sut.SendAsync("prompt", config);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(300));
        Assert.That(captured.Attachments, Has.Count.EqualTo(2));
        Assert.That(captured.Attachments![0].Path, Is.EqualTo(firstAttachmentPath));
        Assert.That(captured.Attachments[0].DisplayName, Is.EqualTo("data"));
    }

    [Test]
    [Property("IntegrationPointId", "T-5-02")]
    public async Task SendAsync_UsesProviderTimeoutWhenRequestTimeoutIsNotSpecified()
    {
        CopilotSdkInvocation? captured = null;
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions
            {
                TimeoutSeconds = 120,
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });

        _ = await sut.SendAsync("prompt", new CopilotSessionConfig());

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(120));
    }

    [TestCase(1, TestName = "T-5-05 TimeoutSeconds minimum positive is accepted")]
    [TestCase(2147483647, TestName = "T-5-06 TimeoutSeconds max int is accepted")]
    public async Task SendAsync_AcceptsBoundaryRequestTimeout(int timeoutSeconds)
    {
        CopilotSdkInvocation? captured = null;
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions
            {
                TimeoutSeconds = 120,
            },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (invocation, _) =>
            {
                captured = invocation;
                return Task.FromResult("ok");
            });

        _ = await sut.SendAsync("prompt", new CopilotSessionConfig { TimeoutSeconds = timeoutSeconds });

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TimeoutSeconds, Is.EqualTo(timeoutSeconds));
    }

    [Test]
    public void SendAsync_RejectsNonPositiveRequestTimeoutWithInvalidRequest()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.SendAsync("prompt", new CopilotSessionConfig { TimeoutSeconds = 0 }));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.ProviderName, Is.EqualTo("GitHubCopilot"));
        Assert.That(ex.Message, Is.EqualTo("TimeoutSeconds must be greater than zero."));
    }

    [Test]
    public void SendAsync_RejectsRelativeAttachmentPathWithInvalidRequest()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions(),
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var ex = Assert.ThrowsAsync<InvalidRequestException>(
            async () => await sut.SendAsync(
                "prompt",
                new CopilotSessionConfig
                {
                    Attachments = [new FileAttachment { Path = "relative.txt" }],
                }));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.ProviderName, Is.EqualTo("GitHubCopilot"));
        Assert.That(ex.Message, Is.EqualTo("Attachment path must be an absolute path."));
    }

    [Test]
    public void BuildCliDiagnosticsSummary_DoesNotExposeRawPathValues()
    {
        var sut = new GitHubCopilotSdkWrapper(
            new GitHubCopilotProviderOptions { CliPath = "/usr/local/bin/copilot" },
            NullLogger<GitHubCopilotSdkWrapper>.Instance,
            listModelsCore: null,
            sendCore: (_, _) => Task.FromResult("ok"));

        var summary = sut.BuildCliDiagnosticsSummary();
        var detail = sut.BuildCliDiagnosticsDetail();

        Assert.That(summary, Does.Contain("CliPath=/usr/local/bin/copilot"));
        Assert.That(summary, Does.Contain("PathEntryCount="));
        Assert.That(summary, Does.Not.Contain("KnownLocations="));
        Assert.That(detail, Does.Contain("PathPreview="));
        Assert.That(detail, Does.Contain("KnownLocations="));
    }

    [Test]
    public void TryTranslateSdkTraceMessage_SuppressesSessionEventNotificationNoise()
    {
        var translated = GitHubCopilotSdkWrapper.TryTranslateSdkTraceMessage(
            "[LoggerTraceSource] Received notification for method \"session.event\".",
            out _,
            out var message);

        Assert.That(translated, Is.False);
        Assert.That(message, Is.Null);
    }

    [Test]
    public void TryTranslateSdkTraceMessage_TranslatesPermissionRequestTrace()
    {
        var translated = GitHubCopilotSdkWrapper.TryTranslateSdkTraceMessage(
            "[LoggerTraceSource] {\"id\":22,\"method\":\"session.permissions.handlePendingPermissionRequest\"}",
            out var level,
            out var message);

        Assert.That(translated, Is.True);
        Assert.That(level, Is.EqualTo(LogLevel.Information));
        Assert.That(message, Does.Contain("pending permission request"));
        Assert.That(message, Does.Contain("RequestId=22"));
    }

    [Test]
    public void TryTranslateSdkTraceMessage_ExtractsStreamTextFromSessionEvent()
    {
        var translated = GitHubCopilotSdkWrapper.TryTranslateSdkTraceMessage(
            "[LoggerTraceSource] {\"id\":null,\"method\":\"session.event\",\"params\":{\"delta\":{\"text\":\"hello from stream\"}}}",
            out var level,
            out var message);

        Assert.That(translated, Is.True);
        Assert.That(level, Is.EqualTo(LogLevel.Debug));
        Assert.That(message, Is.EqualTo("Copilot SDK stream text received. Length=17."));
    }

    [Test]
    public void CopilotSdkTraceLogger_LogsMappedInformationEvenWhenOriginalDebugIsDisabled()
    {
        var loggerMock = new Mock<ILogger>();
        loggerMock
            .Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
            .Returns<LogLevel>(level => level == LogLevel.Information);

        var sut = new GitHubCopilotSdkWrapper.CopilotSdkTraceLogger(loggerMock.Object);

        sut.Log(
            LogLevel.Debug,
            new EventId(22, "sdk"),
            "[LoggerTraceSource] {\"id\":22,\"method\":\"session.permissions.handlePendingPermissionRequest\"}",
            null,
            static (state, _) => state);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) =>
                    value.ToString()!.Contains("pending permission request", StringComparison.Ordinal)
                    && value.ToString()!.Contains("RequestId=22", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void TryTranslateSdkTraceMessage_SuppressesSessionEventWithoutReadableText()
    {
        var translated = GitHubCopilotSdkWrapper.TryTranslateSdkTraceMessage(
            "[LoggerTraceSource] {\"id\":null,\"method\":\"session.event\"}",
            out _,
            out var message);

        Assert.That(translated, Is.False);
        Assert.That(message, Is.Null);
    }

    // --- T-6-xx: CLI 解決戦略の改善 ---
    // BuildCliDiagnostics は private メソッドであり、実 SDK の CLI 解決フローに依存するため
    // 通常の CI 環境では自動実行対象外とする。実 CLI 環境での手動確認または opt-in E2E で確認すること。

    [Test]
    [Property("IntegrationPointId", "T-6-01")]
    [Explicit("BuildCliDiagnostics が private かつ実 CLI 未検出を通常 CI で安定再現できないため通常実行対象外")]
    public void UT_IT_T_6_01__CliNotFoundErrorMessageContainsOsInfo()
    {
        // CLI 未検出時の例外メッセージに Environment.OSVersion 相当の情報が含まれることを確認する想定。
        // 実 SDK 依存のため、手動確認または opt-in E2E で対応すること。
        Assert.Inconclusive("実 CLI 環境が必要なため通常 CI では実行しない。");
    }

    [Test]
    [Property("IntegrationPointId", "T-6-02")]
    [Explicit("BuildCliDiagnostics が private かつ実 CLI 未検出を通常 CI で安定再現できないため通常実行対象外")]
    public void UT_IT_T_6_02__CliNotFoundErrorMessageContainsPathInfo()
    {
        // CLI 未検出時の公開例外メッセージには PATH の件数要約のみが含まれることを確認する想定。
        // 実 SDK 依存のため、手動確認または opt-in E2E で対応すること。
        Assert.Inconclusive("実 CLI 環境が必要なため通常 CI では実行しない。");
    }

    [Test]
    [Property("IntegrationPointId", "T-6-03")]
    [Explicit("BuildCliDiagnostics が private かつ実 CLI 未検出を通常 CI で安定再現できないため通常実行対象外")]
    public void UT_IT_T_6_03__CliNotFoundErrorMessageContainsCandidatePaths()
    {
        // CLI 未検出時の詳細ログに既知候補パス（プラットフォーム別）が含まれることを確認する想定。
        // 実 SDK 依存のため、手動確認または opt-in E2E で対応すること。
        Assert.Inconclusive("実 CLI 環境が必要なため通常 CI では実行しない。");
    }

    [Test]
    [Property("IntegrationPointId", "T-6-04")]
    [Explicit("SDK 内部の CLI 探索スキップ挙動は実 SDK / CLI 起動を伴うため通常実行対象外")]
    public void UT_IT_T_6_04__ExplicitCliPathSkipsDiscovery()
    {
        // GitHubCopilotProviderOptions.CliPath を指定した場合に候補パス探索をスキップすることを確認する想定。
        // GitHubCopilotSdkWrapper.GetOrCreateClientAsync で CliPath を直渡しする実装はあるが、
        // 探索スキップの外形確認は実 CLI / SDK 起動を伴うため通常 CI では実行しない。
        Assert.Inconclusive("実 SDK 起動が必要なため通常 CI では実行しない。");
    }

    private static string GetAbsoluteTestPath(string fileName)
        => Path.Combine(Path.GetTempPath(), "meai-ghcp-tests", fileName);
}
