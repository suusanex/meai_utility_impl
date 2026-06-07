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
            ProviderOverride = new ProviderOverrideOptions { Type = "openai", ApiKey = "secret" },
            WorkingDirectory = "D:\\work",
            AvailableTools = ["read_file"],
            Agent = "reviewer",
        });

        Assert.Equal("ok", response.Text);
        Assert.Equal("hello", wrapper.LastPrompt);
        Assert.NotNull(wrapper.LastConfig);
        Assert.Equal("gpt-5", wrapper.LastConfig.ModelId);
        Assert.Equal(ReasoningEffortLevel.High, wrapper.LastConfig.ReasoningEffort);
        Assert.Equal(42, wrapper.LastConfig.TimeoutSeconds);
        Assert.Single(wrapper.LastConfig.Attachments!);
        Assert.Equal("skills", Assert.Single(wrapper.LastConfig.SkillDirectories!));
        Assert.Equal("legacy", Assert.Single(wrapper.LastConfig.DisabledSkills!));
        Assert.Equal("openai", wrapper.LastConfig.ProviderOverride!.Type);
        Assert.Equal("D:\\work", wrapper.LastConfig.AdvancedOptions["copilot.workingDirectory"]);
        Assert.Equal("reviewer", wrapper.LastConfig.AdvancedOptions["copilot.agent"]);
    }

    [Fact]
    public async Task StreamTurnAsync_MapsWrapperUpdates()
    {
        var wrapper = new ScriptedCopilotSdkWrapper { SupportsStreaming = true };
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(CopilotStreamingUpdateKind.Delta, TextDelta: "a", DeltaCount: 1, AccumulatedLength: 1));
        wrapper.StreamingUpdates.Add(new CopilotStreamingUpdate(CopilotStreamingUpdateKind.Completed, FinalText: "a", DeltaCount: 1, AccumulatedLength: 1));
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
            },
            update =>
            {
                Assert.Equal(GitHubCopilotStreamingUpdateKind.Completed, update.Kind);
                Assert.Equal("a", update.FinalText);
            });
        Assert.True(wrapper.LastConfig!.Streaming);
    }

    [Fact]
    public async Task SendTurnAsync_UnknownModelFailsFast()
    {
        var wrapper = new ScriptedCopilotSdkWrapper
        {
            Models = [new CopilotModelInfo("gpt-4.1", true)],
        };
        var client = new GitHubCopilotAgentClient(wrapper, new GitHubCopilotOptions());

        var ex = await Assert.ThrowsAsync<RuntimeInvalidRequestException>(() =>
            client.SendTurnAsync(new GitHubCopilotAgentRequest { Prompt = "hello", ModelId = "gpt-5" }));

        Assert.Equal("GitHubCopilot", ex.RuntimeName);
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
