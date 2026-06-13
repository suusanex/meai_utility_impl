using MultiCodingAgentFacade.Core.Exceptions;
using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot.Abstractions;
using MultiCodingAgentFacade.GitHubCopilot.Configuration;
using MultiCodingAgentFacade.GitHubCopilot.Options;
using MultiCodingAgentFacade.GitHubCopilot.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MultiCodingAgentFacade.GitHubCopilot.Tests;

public sealed class GitHubCopilotAgentClientTests
{
    [Fact]
    public async Task SendTurnAsync_MapsTypedRequestToWrapperConfig()
    {
        var wrapper = new ScriptedCopilotSdkWrapper { ResponseText = "ok" };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());
        var attachmentPath = Path.Combine(Path.GetTempPath(), "copilot.txt");

        var response = await client.SendTurnAsync(new GitHubCopilotAgentRequest
        {
            Prompt = "hello",
            ModelId = "gpt-5",
            ReasoningEffort = ReasoningEffortLevel.High,
            TimeoutSeconds = 42,
            Attachments = [new FileAttachment { Path = attachmentPath, DisplayName = "copilot.txt" }],
            SkillDirectories = ["skills"],
            DisabledSkills = ["legacy"],
            ModelProvider = new GitHubCopilotModelProviderOptions { Type = "openai", BaseUrl = "https://api.openai.com/v1", ApiKey = "secret" },
            WorkingDirectory = "D:\\work",
            AvailableTools = ["read_file"],
            Agent = "reviewer",
            PermissionHandling = GitHubCopilotPermissionHandlingMode.DenyAll,
        });

        Assert.Equal("ok", response.Text);
        Assert.Equal("Completed", response.FinishStatus);
        Assert.Equal("fake diagnostics", response.DiagnosticsSummary);
        Assert.Equal("GitHubCopilot", response.RuntimeName);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
        Assert.True(response.ElapsedTime >= TimeSpan.Zero);
        Assert.True((bool)response.SdkMetadata!["fake.sdk"]!);
        Assert.Equal("hello", wrapper.LastPrompt);
        Assert.NotNull(wrapper.LastConfig);
        Assert.Equal("gpt-5", wrapper.LastConfig.ModelId);
        Assert.Equal(ReasoningEffortLevel.High, wrapper.LastConfig.ReasoningEffort);
        Assert.Equal(42, wrapper.LastConfig.TimeoutSeconds);
        Assert.Single(wrapper.LastConfig.Attachments!);
        Assert.Equal("skills", Assert.Single(wrapper.LastConfig.SkillDirectories!));
        Assert.Equal("legacy", Assert.Single(wrapper.LastConfig.DisabledSkills!));
        Assert.Equal("openai", wrapper.LastConfig.ModelProvider!.Type);
        Assert.Equal("https://api.openai.com/v1", wrapper.LastConfig.ModelProvider.BaseUrl);
        Assert.Equal(GitHubCopilotPermissionHandlingMode.DenyAll, wrapper.LastConfig.PermissionHandling);
        Assert.Equal("D:\\work", wrapper.LastConfig.AdvancedOptions["copilot.workingDirectory"]);
        Assert.Equal("reviewer", wrapper.LastConfig.AdvancedOptions["copilot.agent"]);
    }

    [Fact]
    public async Task StreamTurnAsync_MapsWrapperUpdates()
    {
        var wrapper = new ScriptedCopilotSdkWrapper { SupportsStreaming = true };
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(CopilotStreamingUpdateKind.Delta, TextDelta: "a", DeltaCount: 1, AccumulatedLength: 1));
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(CopilotStreamingUpdateKind.Completed, FinalText: "a", DeltaCount: 1, AccumulatedLength: 1, FinishStatus: "Completed", DiagnosticsSummary: "done"));
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var updates = new List<GitHubCopilotStreamingUpdate>();
        await foreach (var update in client.StreamTurnAsync(new GitHubCopilotAgentRequest { Prompt = "hello", ModelId = "gpt-5" }))
        {
            updates.Add(update);
        }

        Assert.Collection(
            updates,
            update =>
            {
                Assert.Equal(GitHubCopilotStreamingUpdateKind.Delta, update.Kind);
                Assert.Equal("a", update.TextDelta);
                Assert.Equal("GitHubCopilot", update.RuntimeName);
                Assert.False(string.IsNullOrWhiteSpace(update.TraceId));
                Assert.False(string.IsNullOrWhiteSpace(update.RequestId));
            },
            update =>
            {
                Assert.Equal(GitHubCopilotStreamingUpdateKind.Completed, update.Kind);
                Assert.Equal("a", update.FinalText);
                Assert.Equal("Completed", update.FinishStatus);
                Assert.Equal("done", update.DiagnosticsSummary);
                Assert.Equal(updates[0].TraceId, update.TraceId);
                Assert.Equal(updates[0].RequestId, update.RequestId);
            });
        Assert.True(wrapper.LastConfig!.Streaming);
    }

    [Fact]
    public async Task StreamTurnAsync_PreservesCompletedFinalTextAfterProgressOnly()
    {
        var wrapper = new ScriptedCopilotSdkWrapper { SupportsStreaming = true };
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(
            CopilotStreamingUpdateKind.Progress,
            DeltaCount: 0,
            AccumulatedLength: 0,
            SdkMetadata: new Dictionary<string, object?> { ["sdk.ignoredDeltaReason"] = "event-type" }));
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(
            CopilotStreamingUpdateKind.Completed,
            FinalText: "final text",
            DeltaCount: 0,
            AccumulatedLength: 0,
            FinishStatus: "Completed",
            DiagnosticsSummary: "done"));
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var updates = new List<GitHubCopilotStreamingUpdate>();
        await foreach (var update in client.StreamTurnAsync(new GitHubCopilotAgentRequest { Prompt = "hello", ModelId = "gpt-5" }))
        {
            updates.Add(update);
        }

        Assert.Collection(
            updates,
            update =>
            {
                Assert.Equal(GitHubCopilotStreamingUpdateKind.Progress, update.Kind);
                Assert.Null(update.TextDelta);
                Assert.Equal("event-type", update.SdkMetadata!["sdk.ignoredDeltaReason"]);
            },
            update =>
            {
                Assert.Equal(GitHubCopilotStreamingUpdateKind.Completed, update.Kind);
                Assert.Equal("final text", update.FinalText);
                Assert.Equal(0, update.DeltaCount);
                Assert.Equal(0, update.AccumulatedLength);
            });
    }

    [Fact]
    public async Task SendTurnAsync_UnknownModelFailsFast()
    {
        var wrapper = new ScriptedCopilotSdkWrapper
        {
            Models = [new CopilotModelInfo("gpt-4.1", ["low"], "low")],
        };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var ex = await Assert.ThrowsAsync<RuntimeInvalidRequestException>(() =>
            client.SendTurnAsync(new GitHubCopilotAgentRequest { Prompt = "hello", ModelId = "gpt-5" }));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
    }

    [Fact]
    public async Task SendTurnAsync_UnsupportedReasoningEffortFailsFast()
    {
        var wrapper = new ScriptedCopilotSdkWrapper
        {
            Models = [new CopilotModelInfo("gpt-5", ["low"], "low")],
        };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var ex = await Assert.ThrowsAsync<RuntimeFeatureNotSupportedException>(() =>
            client.SendTurnAsync(new GitHubCopilotAgentRequest
            {
                Prompt = "hello",
                ModelId = "gpt-5",
                ReasoningEffort = ReasoningEffortLevel.High,
            }));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Equal("ReasoningEffort", ex.FeatureName);
    }

    [Fact]
    public void BuildInvocation_ValidatesModelProviderBeforeSdkSessionCreation()
    {
        var config = new CopilotSessionConfig
        {
            ModelProvider = new GitHubCopilotModelProviderOptions
            {
                Type = "azure",
                BaseUrl = "https://example.openai.azure.com",
                ApiKey = "secret",
            },
        };

        var ex = Assert.Throws<RuntimeInvalidRequestException>(() =>
            GitHubCopilotSdkWrapper.BuildInvocation("hello", config, new GitHubCopilotOptions()));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("AzureApiVersion", ex.Message);
    }

    [Fact]
    public void BuildInvocation_RejectsUnsupportedAdvancedOptions()
    {
        var config = new CopilotSessionConfig();
        config.AdvancedOptions["copilot.unsupported"] = true;

        var ex = Assert.Throws<RuntimeInvalidRequestException>(() =>
            GitHubCopilotSdkWrapper.BuildInvocation("hello", config, new GitHubCopilotOptions()));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Contains("copilot.unsupported", ex.Message);
    }

    [Fact]
    public void BuildInvocation_MapsPermissionHandlingDefaultAndExplicitMode()
    {
        var defaultInvocation = GitHubCopilotSdkWrapper.BuildInvocation(
            "hello",
            new CopilotSessionConfig(),
            new GitHubCopilotOptions());
        var explicitInvocation = GitHubCopilotSdkWrapper.BuildInvocation(
            "hello",
            new CopilotSessionConfig { PermissionHandling = GitHubCopilotPermissionHandlingMode.NoResult },
            new GitHubCopilotOptions());

        Assert.Equal(GitHubCopilotPermissionHandlingMode.ApproveAll, defaultInvocation.PermissionHandling);
        Assert.Equal(GitHubCopilotPermissionHandlingMode.NoResult, explicitInvocation.PermissionHandling);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsSupportedAndDefaultReasoningEfforts()
    {
        var wrapper = new ScriptedCopilotSdkWrapper
        {
            Models = [new CopilotModelInfo("gpt-5", ["low", "medium"], "medium")],
        };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var model = Assert.Single(await client.ListModelsAsync());

        Assert.Equal("gpt-5", model.ModelId);
        Assert.Equal(["low", "medium"], model.SupportedReasoningEfforts);
        Assert.Equal("medium", model.DefaultReasoningEffort);
        Assert.True(model.SupportsReasoningEffort);
    }

    [Fact]
    public async Task StreamTurnAsync_UnsupportedWrapperFailsFast()
    {
        var wrapper = new ScriptedCopilotSdkWrapper { SupportsStreaming = false };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var ex = await Assert.ThrowsAsync<RuntimeFeatureNotSupportedException>(async () =>
        {
            await foreach (var _ in client.StreamTurnAsync(new GitHubCopilotAgentRequest { Prompt = "hello", ModelId = "gpt-5" }))
            {
            }
        });

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
        Assert.Equal("Streaming", ex.FeatureName);
    }

    [Fact]
    public void AddGitHubCopilotAgentRuntime_RegistersProductionWrapper()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiCodingAgentFacade:GitHubCopilot:ModelId"] = "gpt-5",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddGitHubCopilotAgentRuntime(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<GitHubCopilotSdkWrapper>(provider.GetRequiredService<ICopilotSdkWrapper>());
        Assert.NotNull(provider.GetRequiredService<GitHubCopilotAgentClient>());
    }
}
