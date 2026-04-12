using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.Samples;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.IntegrationTests.Samples;

public class InteractiveChatSampleTests
{
    [Test]
    public async Task InteractiveChatSample_PreservesConversationHistory()
    {
        var chatClient = new RecordingChatClient();
        using var input = new StringReader("hello\nsecond turn\n/exit\n");
        using var output = new StringWriter();

        await InteractiveChatSample.RunAsync(chatClient, input, output);

        Assert.That(chatClient.Calls, Has.Count.EqualTo(2));
        Assert.That(chatClient.Calls[0].Select(static message => $"{NormalizeRole(message.Role)}:{message.Text}").ToArray(),
            Is.EqualTo(new[] { "User:hello" }));
        Assert.That(chatClient.Calls[1].Select(static message => $"{NormalizeRole(message.Role)}:{message.Text}").ToArray(),
            Is.EqualTo(new[] { "User:hello", "Assistant:echo:hello", "User:second turn" }));
        Assert.That(output.ToString(), Does.Contain("Assistant: echo:second turn"));
    }

    private static string NormalizeRole(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return "User";
        }

        if (role == ChatRole.Assistant)
        {
            return "Assistant";
        }

        if (role == ChatRole.System)
        {
            return "System";
        }

        if (role == ChatRole.Tool)
        {
            return "Tool";
        }

        return role.ToString();
    }

    [Test]
    public async Task GitHubCopilotSampleHost_ResolvesConfiguredChatClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiProvider:Provider"] = "GitHubCopilot",
                ["MultiProvider:GitHubCopilot:CliPath"] = "copilot",
                ["MultiProvider:GitHubCopilot:UseLoggedInUser"] = "true",
                ["MultiProvider:GitHubCopilot:ModelId"] = "gpt-5-mini",
            })
            .Build();

        var services = new ServiceCollection();
        GitHubCopilotSampleHost.ConfigureServices(services, configuration);

        var wrapper = new CapturingCopilotSdkWrapper();
        services.AddSingleton<ICopilotSdkWrapper>(wrapper);

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();

        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "hello"),
            new ChatMessage(ChatRole.Assistant, "hi"),
            new ChatMessage(ChatRole.User, "again"),
        ]);

        Assert.That(response.Text, Is.EqualTo("copilot"));
        Assert.That(wrapper.LastPrompt, Is.EqualTo("User: hello\nAssistant: hi\nUser: again"));
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var snapshot = messages.Select(message => new ChatMessage(message.Role, message.Text)).ToArray();
            Calls.Add(snapshot);
            var reply = snapshot.Last().Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"echo:{reply}")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "unused");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingCopilotSdkWrapper : ICopilotSdkWrapper
    {
        public string? LastPrompt { get; private set; }

        public Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotModelInfo>>([new CopilotModelInfo("gpt-5-mini", false)]);

        public Task<string> SendAsync(string prompt, CopilotSessionConfig config, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult("copilot");
        }
    }
}
