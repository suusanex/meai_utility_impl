using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAIChatClientAdapterTests
{
    [Test]
    public async Task GetResponseAsync_ReturnsResponse()
    {
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var options = new ChatOptions { Temperature = 0.5f, MaxOutputTokens = 100 };
        options.StopSequences = ["stop"];

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Message.Text, Does.Contain("OpenAI response"));
    }

    [Test]
    [Property("IntegrationPointId", "T-P-03")]
    public async Task UT_IT_T_P_03__OpenAITimeoutSecondsIsIgnoredAndNoException()
    {
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var options = new ChatOptions();
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = new ConversationExecutionOptions
        {
            TimeoutSeconds = 60,
        };

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(response.Message.Text, Does.Contain("OpenAI response"));
    }

    [Test]
    [Property("IntegrationPointId", "T-P-04")]
    public void UT_IT_T_P_04__OpenAIExceptionIsNotCopilotRuntimeException()
    {
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
        var ext = new ExtensionParameters();
        ext.Set("copilot.mode", "plan");
        var options = new ChatOptions();
        options.AdditionalProperties["meai.extensions"] = ext;

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));

        Assert.That(ex, Is.Not.TypeOf<MeAiUtility.MultiProvider.Exceptions.CopilotRuntimeException>());
    }

    [TestCase("Attachments", TestName = "T-P-01 OpenAI rejects Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 OpenAI rejects SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a OpenAI rejects DisabledSkills")]
    public void GetResponseAsync_RejectsCopilotOnlyExecutionOption(string featureName)
    {
        var sut = new OpenAIChatClientAdapter(new NullLogger<OpenAIChatClientAdapter>());
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
}
