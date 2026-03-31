using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.Logging.Abstractions;

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
}
