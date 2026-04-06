using MeAiUtility.MultiProvider.AzureOpenAI;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Tests;

public class AzureOpenAIChatClientAdapterTests
{
    [Test]
    public async Task GetResponseAsync_ReturnsResponse()
    {
        var sut = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());
        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());
        Assert.That(response.Message.Text, Does.Contain("AzureOpenAI"));
    }

    [TestCase("Attachments", TestName = "T-P-01 AzureOpenAI rejects Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 AzureOpenAI rejects SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a AzureOpenAI rejects DisabledSkills")]
    public void GetResponseAsync_RejectsCopilotOnlyExecutionOption(string featureName)
    {
        var sut = new AzureOpenAIChatClientAdapter(new NullLogger<AzureOpenAIChatClientAdapter>());
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
