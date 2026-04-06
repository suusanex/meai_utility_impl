using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.GitHubCopilot;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.IntegrationTests.ContractTests;

public class ProviderSpecificTests
{
    [Test]
    public async Task OpenAIAndAzureRejectForeignExtensions()
    {
        var ext = new ExtensionParameters();
        ext.Set("copilot.mcp_servers", new { });
        var options = new ChatOptions();
        options.AdditionalProperties["meai.extensions"] = ext;

        var openAi = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var azure = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());

        Assert.That(async () => await openAi.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>());
        Assert.That(async () => await azure.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options), Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>());
    }

    [Test]
    public async Task CopilotAcceptsProviderOverride()
    {
        var wrapper = new PassWrapper();
        var sut = new GitHubCopilotChatClient(new CopilotClientHost(wrapper, new GitHubCopilotProviderOptions(), new NullLogger<CopilotClientHost>()), new GitHubCopilotProviderOptions(), new NullLogger<GitHubCopilotChatClient>());
        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            ProviderOverride = new ProviderOverrideOptions { Type = "openai", BaseUrl = "https://api.openai.com/v1" },
        };

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options);
        Assert.That(response.Message.Text, Is.EqualTo("ok"));
    }

    [TestCase("Attachments", TestName = "T-P-01 non-Copilot providers reject Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 non-Copilot providers reject SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a non-Copilot providers reject DisabledSkills")]
    public void NonCopilotProviders_RejectCopilotOnlyExecutionOptions(string featureName)
    {
        var openAi = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var azure = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());
        var openAiCompatible = new OpenAICompatibleProvider(
            new NullLogger<OpenAICompatibleProvider>(),
            new MeAiUtility.MultiProvider.OpenAI.Options.OpenAICompatibleProviderOptions { BaseUrl = "http://localhost", ModelName = "gpt-4" });

        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = featureName switch
        {
            "Attachments" => new ConversationExecutionOptions
            {
                Attachments =
                [
                    new FileAttachment { Path = @"C:\payload.json" },
                ],
            },
            "SkillDirectories" => new ConversationExecutionOptions
            {
                SkillDirectories = [@"C:\skills"],
            },
            _ => new ConversationExecutionOptions
            {
                DisabledSkills = ["skill-a"],
            },
        };

        var openAiEx = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(async () => await openAi.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options));
        var azureEx = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(async () => await azure.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options));
        var compatibleEx = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(async () => await openAiCompatible.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], options));

        Assert.That(openAiEx!.FeatureName, Is.EqualTo(featureName));
        Assert.That(azureEx!.FeatureName, Is.EqualTo(featureName));
        Assert.That(compatibleEx!.FeatureName, Is.EqualTo(featureName));
    }

    private sealed class PassWrapper : ICopilotSdkWrapper
    {
        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5", true)]);
        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default) => Task.FromResult("ok");
    }
}
