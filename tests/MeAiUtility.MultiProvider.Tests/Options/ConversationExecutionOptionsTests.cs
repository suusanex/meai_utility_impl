using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Tests.Options;

public class ConversationExecutionOptionsTests
{
    [Test]
    public void FromChatOptions_ReturnsNull_WhenAdditionalPropertiesIsNull()
    {
        var options = new ChatOptions();

        var actual = ConversationExecutionOptions.FromChatOptions(options);

        Assert.That(actual, Is.Null);
    }

    [Test]
    public void FromChatOptions_ReadsExecutionOptions()
    {
        var options = new ChatOptions();
        var expected = new ConversationExecutionOptions { ModelId = "gpt-5", Streaming = true };
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = expected;

        var actual = ConversationExecutionOptions.FromChatOptions(options);

        Assert.That(actual, Is.Not.Null);
        Assert.That(actual!.ModelId, Is.EqualTo("gpt-5"));
        Assert.That(actual.Streaming, Is.True);
    }

    [Test]
    public void FromChatOptions_ReadsCopilotSpecificProperties()
    {
        var options = new ChatOptions();
        var expected = new ConversationExecutionOptions
        {
            Attachments =
            [
                new FileAttachment { Path = @"C:\data.json", DisplayName = "data" },
            ],
            SkillDirectories = [@"C:\skills"],
            DisabledSkills = ["skill-a"],
            TimeoutSeconds = 300,
        };
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = expected;

        var actual = ConversationExecutionOptions.FromChatOptions(options);

        Assert.That(actual, Is.Not.Null);
        Assert.That(actual!.Attachments, Has.Count.EqualTo(1));
        Assert.That(actual.Attachments![0].Path, Is.EqualTo(@"C:\data.json"));
        Assert.That(actual.SkillDirectories, Is.EqualTo(new[] { @"C:\skills" }));
        Assert.That(actual.DisabledSkills, Is.EqualTo(new[] { "skill-a" }));
        Assert.That(actual.TimeoutSeconds, Is.EqualTo(300));
    }
}


