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
}
