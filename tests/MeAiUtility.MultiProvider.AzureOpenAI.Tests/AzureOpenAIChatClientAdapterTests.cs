using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class AzureOpenAIChatClientAdapterTests
{
    [Test]
    public async Task GetResponseAsync_ReturnsInjectedResponse()
    {
        var sut = new AzureOpenAIChatClientAdapter(
            new NullLogger<AzureOpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "stubbed azure response"))),
            static (_, _, _) => EmptyUpdates());

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());

        Assert.That(response.Message.Text, Is.EqualTo("stubbed azure response"));
    }

    [Test]
    public void Constructor_RejectsUnsupportedApiVersion()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateSut(apiVersion: "2024-02-15-preview"));

        Assert.That(ex!.Message, Does.Contain("Unsupported Azure OpenAI ApiVersion"));
    }

    private static AzureOpenAIProviderOptions CreateOptions(string apiVersion = "2024-06-01") => new()
    {
        Endpoint = "https://example.openai.azure.com",
        DeploymentName = "gpt-4o-mini",
        ApiVersion = apiVersion,
        Authentication = new AzureAuthenticationOptions
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
        },
    };

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }

    [TestCase("Attachments", TestName = "T-P-01 AzureOpenAI rejects Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 AzureOpenAI rejects SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a AzureOpenAI rejects DisabledSkills")]
    public void GetResponseAsync_RejectsCopilotOnlyExecutionOption(string featureName)
    {
        var sut = CreateSut();
        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = featureName switch
        {
            "Attachments" => new ConversationExecutionOptions
            {
                Attachments =
                [
                    new FileAttachment { Path = @"C:\data.json" },
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

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));
        Assert.That(ex!.FeatureName, Is.EqualTo(featureName));
    }

    private static AzureOpenAIChatClientAdapter CreateSut(string responseText = "stubbed azure response")
        => new(
            new NullLogger<AzureOpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))),
            static (_, _, _) => EmptyUpdates());

    private static AzureOpenAIChatClientAdapter CreateSut(string apiVersion, string responseText = "stubbed azure response")
        => new(
            new NullLogger<AzureOpenAIChatClientAdapter>(),
            CreateOptions(apiVersion),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))),
            static (_, _, _) => EmptyUpdates());
}
