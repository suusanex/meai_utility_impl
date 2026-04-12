using MeAiUtility.MultiProvider.Options;
using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAIChatClientAdapterTests
{
    [Test]
    public void ToOfficialChatOptions_PreservesResponseFormat()
    {
        var options = new ChatOptions();
        options.ResponseFormat = ChatResponseFormat.Json;

        var official = OpenAIOfficialBridge.ToOfficialChatOptions(options, "gpt-4o-mini");

        Assert.That(official.ResponseFormat, Is.Not.Null);
    }

    [Test]
    public async Task GetResponseAsync_ReturnsInjectedResponse()
    {
        var sut = new OpenAIChatClientAdapter(
            new NullLogger<OpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "stubbed openai response"))),
            static (_, _, _) => EmptyUpdates());

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());

        Assert.That(response.Text, Is.EqualTo("stubbed openai response"));
    }

    private static OpenAIProviderOptions CreateOptions() => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        ModelName = "gpt-4o-mini",
    };

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }

    [Test]
    [Property("IntegrationPointId", "T-P-03")]
    public async Task UT_IT_T_P_03__OpenAITimeoutSecondsIsIgnoredAndNoException()
    {
        var sut = CreateSut("OpenAI response (gpt-4o-mini)");
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            TimeoutSeconds = 60,
        };

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Text, Does.Contain("OpenAI response"));
    }

    [Test]
    [Property("IntegrationPointId", "T-P-04")]
    public void UT_IT_T_P_04__OpenAIExceptionIsNotCopilotRuntimeException()
    {
        var sut = CreateSut();
        var ext = new ExtensionParameters();
        ext.Set("copilot.mode", "plan");
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex, Is.Not.TypeOf<MeAiUtility.MultiProvider.Exceptions.CopilotRuntimeException>());
    }

    [TestCase("Attachments", TestName = "T-P-01 OpenAI rejects Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 OpenAI rejects SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a OpenAI rejects DisabledSkills")]
    public void GetResponseAsync_RejectsCopilotOnlyExecutionOption(string featureName)
    {
        var sut = CreateSut();
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = featureName switch
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

    private static OpenAIChatClientAdapter CreateSut(string responseText = "stubbed openai response")
        => new(
            new NullLogger<OpenAIChatClientAdapter>(),
            CreateOptions(),
            (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))),
            static (_, _, _) => EmptyUpdates());

}


