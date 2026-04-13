using MeAiUtility.MultiProvider.OpenAI;
using MeAiUtility.MultiProvider.OpenAI.Options;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.OpenAI.Tests;

public class OpenAICompatibleProviderTests
{
    [Test]
    public void CreateClientOptions_AppliesTimeoutSecondsToNetworkTimeout()
    {
        var options = new OpenAICompatibleProviderOptions
        {
            ModelName = "gpt-4",
            BaseUrl = "http://localhost",
            TimeoutSeconds = 300,
        };

        var clientOptions = OpenAIOfficialBridge.CreateClientOptions(options.BaseUrl, organizationId: null, options.TimeoutSeconds);

        Assert.That(clientOptions.NetworkTimeout, Is.EqualTo(TimeSpan.FromSeconds(300)));
    }

    [Test]
    public void CreateClientOptions_ClampsNonPositiveTimeoutSecondsToOneSecond()
    {
        var zeroTimeoutClientOptions = OpenAIOfficialBridge.CreateClientOptions("http://localhost", organizationId: null, timeoutSeconds: 0);
        var negativeTimeoutClientOptions = OpenAIOfficialBridge.CreateClientOptions("http://localhost", organizationId: null, timeoutSeconds: -1);

        Assert.That(zeroTimeoutClientOptions.NetworkTimeout, Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(negativeTimeoutClientOptions.NetworkTimeout, Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task NormalizeOptions_PreservesResponseFormat()
    {
        ChatOptions? capturedOptions = null;
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost" };
        var sut = new OpenAICompatibleProvider(
            new NullLogger<OpenAICompatibleProvider>(),
            opts,
            (_, optionsArg, _) =>
            {
                capturedOptions = optionsArg;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            },
            static (_, _, _) => EmptyUpdates());

        var options = new ChatOptions();
        options.ResponseFormat = ChatResponseFormat.Json;

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options);

        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.ResponseFormat, Is.Not.Null);
    }

    [Test]
    public async Task AppliesModelMapping()
    {
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost", ModelMapping = new() { ["gpt-4"] = "mapped" } };
        ConversationExecutionOptions? capturedExecution = null;
        var sut = new OpenAICompatibleProvider(
            new NullLogger<OpenAICompatibleProvider>(),
            opts,
            (_, optionsArg, _) =>
            {
                capturedExecution = ConversationExecutionOptions.FromChatOptions(optionsArg);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"model={capturedExecution?.ModelId}")));
            },
            static (_, _, _) => EmptyUpdates());

        var execution = new ConversationExecutionOptions { ModelId = "gpt-4" };
        var chatOptions = new ChatOptions();
        (chatOptions.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = execution;

        var response = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], chatOptions);

        Assert.That(response.Text, Does.Contain("mapped"));
        Assert.That(capturedExecution, Is.Not.Null);
        Assert.That(capturedExecution!.ModelId, Is.EqualTo("mapped"));
    }

    [Test]
    public void RejectsForeignExtensionPrefix()
    {
        var opts = new OpenAICompatibleProviderOptions { ModelName = "gpt-4", BaseUrl = "http://localhost" };
        var sut = CreateSut(opts);
        var ext = new MeAiUtility.MultiProvider.Options.ExtensionParameters();
        ext.Set("azure.data_sources", new[] { 1 });
        var chatOptions = new ChatOptions();
        (chatOptions.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;

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
        var sut = CreateSut(opts);
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

    private static OpenAICompatibleProvider CreateSut(OpenAICompatibleProviderOptions options)
        => new(
            new NullLogger<OpenAICompatibleProvider>(),
            options,
            static (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))),
            static (_, _, _) => EmptyUpdates());

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyUpdates()
    {
        yield break;
    }

}


