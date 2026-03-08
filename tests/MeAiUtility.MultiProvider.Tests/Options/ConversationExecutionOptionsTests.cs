using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Tests.Options;

public class ConversationExecutionOptionsTests
{
    [Test]
    public void FromChatOptions_ReadsExecutionOptions()
    {
        var options = new ChatOptions();
        var expected = new ConversationExecutionOptions { ModelId = "gpt-5", Streaming = true };
        options.AdditionalProperties[ConversationExecutionOptions.PropertyName] = expected;

        var actual = ConversationExecutionOptions.FromChatOptions(options);

        Assert.That(actual, Is.Not.Null);
        Assert.That(actual!.ModelId, Is.EqualTo("gpt-5"));
        Assert.That(actual.Streaming, Is.True);
    }
}
