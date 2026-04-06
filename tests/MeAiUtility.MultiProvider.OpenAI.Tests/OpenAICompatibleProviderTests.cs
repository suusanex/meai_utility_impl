using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAICompatibleProviderTests
{
    [Test]
    public async Task AppliesModelMapping()
    {
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost", ModelMapping = new() { ["gpt-4"] = "mapped" } };
        var sut = new OpenAICompatibleProvider(new NullLogger<OpenAICompatibleProvider>(), opts);
        var execution = new MeAiUtility.MultiProvider.Options.ConversationExecutionOptions { ModelId = "gpt-4" };
        var chatOptions = new ChatOptions();
        chatOptions.AdditionalProperties[MeAiUtility.MultiProvider.Options.ConversationExecutionOptions.PropertyName] = execution;

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], chatOptions);

        Assert.That(response.Message.Text, Does.Contain("mapped"));
    }

    [Test]
    public void RejectsForeignExtensionPrefix()
    {
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost" };
        var sut = new OpenAICompatibleProvider(new NullLogger<OpenAICompatibleProvider>(), opts);
        var ext = new MeAiUtility.MultiProvider.Options.ExtensionParameters();
        ext.Set("azure.data_sources", new[] { 1 });
        var chatOptions = new ChatOptions();
        chatOptions.AdditionalProperties["meai.extensions"] = ext;

        Assert.That(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], chatOptions),
            Throws.InstanceOf<MeAiUtility.MultiProvider.Exceptions.InvalidRequestException>());
    }

    [TestCase("Attachments", TestName = "T-P-01 OpenAICompatible rejects Attachments")]
    [TestCase("SkillDirectories", TestName = "T-P-02 OpenAICompatible rejects SkillDirectories")]
    [TestCase("DisabledSkills", TestName = "T-P-02a OpenAICompatible rejects DisabledSkills")]
    public void RejectsCopilotOnlyExecutionOption(string featureName)
    {
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost" };
        var sut = new OpenAICompatibleProvider(new NullLogger<OpenAICompatibleProvider>(), opts);
        var options = new ChatOptions();
        options.AdditionalProperties[MeAiUtility.MultiProvider.Options.ConversationExecutionOptions.PropertyName] = featureName switch
        {
            "Attachments" => new MeAiUtility.MultiProvider.Options.ConversationExecutionOptions
            {
                Attachments =
                [
                    new MeAiUtility.MultiProvider.Options.FileAttachment { Path = @"C:\data.json" },
                ],
            },
            "SkillDirectories" => new MeAiUtility.MultiProvider.Options.ConversationExecutionOptions
            {
                SkillDirectories = [@"C:\skills"],
            },
            _ => new MeAiUtility.MultiProvider.Options.ConversationExecutionOptions
            {
                DisabledSkills = ["skill-a"],
            },
        };

        var ex = Assert.ThrowsAsync<MeAiUtility.MultiProvider.Exceptions.NotSupportedException>(
            async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options));
        Assert.That(ex!.FeatureName, Is.EqualTo(featureName));
    }
}
