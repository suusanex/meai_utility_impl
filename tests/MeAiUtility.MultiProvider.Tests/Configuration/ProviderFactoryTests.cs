using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeAiUtility.MultiProvider.Tests.Configuration;

public class ProviderFactoryTests
{
    [Test]
    public void Create_ResolvesConfiguredProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient, FakeClient>();
        services.AddSingleton(typeof(FakeClient));
        services.AddSingleton(new ProviderRegistry());
        services.AddSingleton<IOptions<MultiProviderOptions>>(Microsoft.Extensions.Options.Options.Create(new MultiProviderOptions { Provider = "OpenAI", OpenAI = new object() }));
        services.AddSingleton<IProviderFactory, ProviderFactory>();

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<ProviderRegistry>().Register("OpenAI", typeof(FakeClient));

        var factory = sp.GetRequiredService<IProviderFactory>();
        var client = factory.Create();

        Assert.That(client, Is.TypeOf<FakeClient>());
    }

    [Test]
    public void Create_ResolvesConfiguredCodexProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient, FakeClient>();
        services.AddSingleton(typeof(FakeClient));
        services.AddSingleton(new ProviderRegistry());
        services.AddSingleton<IOptions<MultiProviderOptions>>(Microsoft.Extensions.Options.Options.Create(new MultiProviderOptions { Provider = "CodexAppServer", CodexAppServer = new object() }));
        services.AddSingleton<IProviderFactory, ProviderFactory>();

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<ProviderRegistry>().Register("CodexAppServer", typeof(FakeClient));

        var factory = sp.GetRequiredService<IProviderFactory>();
        var client = factory.Create();

        Assert.That(client, Is.TypeOf<FakeClient>());
    }

    private sealed class FakeClient : IChatClient, IProviderCapabilities
    {
        public bool SupportsReasoningEffort => true;
        public bool SupportsStreaming => true;
        public bool SupportsModelDiscovery => true;
        public bool SupportsEmbeddings => true;
        public bool SupportsProviderOverride => true;
        public bool SupportsExtensionParameters => true;
        public bool IsSupported(FeatureName featureName) => true;
        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }
    }
}
