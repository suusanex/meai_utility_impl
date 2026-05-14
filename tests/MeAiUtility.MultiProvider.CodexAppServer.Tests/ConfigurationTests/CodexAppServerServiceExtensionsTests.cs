using System.Text.Json;
using MeAiUtility.MultiProvider.CodexAppServer;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Configuration;
using MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;
using MeAiUtility.MultiProvider.CodexAppServer.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.ConfigurationTests;

public class CodexAppServerServiceExtensionsTests
{
    [Test]
    public void AddCodexAppServer_RegistersSingletonServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddCodexAppServer(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var firstClient = provider.GetRequiredService<CodexAppServerChatClient>();
        var secondClient = provider.GetRequiredService<CodexAppServerChatClient>();
        var firstFactory = provider.GetRequiredService<ICodexTransportFactory>();
        var secondFactory = provider.GetRequiredService<ICodexTransportFactory>();
        var firstStore = provider.GetRequiredService<ICodexThreadStore>();
        var secondStore = provider.GetRequiredService<ICodexThreadStore>();
        var firstRegistry = provider.GetRequiredService<ICodexThreadRegistry>();
        var secondRegistry = provider.GetRequiredService<ICodexThreadRegistry>();

        Assert.That(secondClient, Is.SameAs(firstClient));
        Assert.That(secondFactory, Is.SameAs(firstFactory));
        Assert.That(secondStore, Is.SameAs(firstStore));
        Assert.That(secondRegistry, Is.SameAs(firstRegistry));
    }

    [Test]
    public void AddCodexAppServer_ThrowsOnNullConfiguration()
    {
        var services = new ServiceCollection();
        Assert.That(() => services.AddCodexAppServer(null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public async Task AddCodexAppServer_ResolvedChatClient_UsesRegisteredThreadStore()
    {
        var seededRecord = new CodexThreadRecord(
            "service-extension-key",
            "thread-from-store",
            "stored-thread",
            @"D:\repo",
            "gpt-5.5-codex",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var trackingStore = new TrackingThreadStore(seededRecord);
        var transport = CreateTransportForStoredThread();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddCodexAppServer(BuildConfiguration(
            ("MultiProvider:CodexAppServer:ThreadReusePolicy", "ReuseOrCreateByKey"),
            ("MultiProvider:CodexAppServer:ThreadKey", seededRecord.ThreadKey)));
        services.AddSingleton<ICodexThreadStore>(trackingStore);
        services.AddSingleton<ICodexTransportFactory>(new StubCodexTransportFactory(transport));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<CodexAppServerChatClient>();
        _ = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(trackingStore.TryGetByKeyCallCount, Is.EqualTo(1));
        Assert.That(trackingStore.SaveCallCount, Is.EqualTo(1));
        Assert.That(HasRequest(transport, "thread/start"), Is.False);
    }

    private static IConfiguration BuildConfiguration()
    {
        return BuildConfiguration([]);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] overrides)
    {
        var configuration = new ConfigurationManager();
        var values = new Dictionary<string, string?>
        {
            ["MultiProvider:CodexAppServer:CodexCommand"] = "codex",
            ["MultiProvider:CodexAppServer:Transport"] = "stdio",
            ["MultiProvider:CodexAppServer:ApprovalPolicy"] = "never",
            ["MultiProvider:CodexAppServer:SandboxMode"] = "workspace-write",
        };

        foreach (var kv in values)
        {
            configuration[kv.Key] = kv.Value;
        }

        foreach (var (key, value) in overrides)
        {
            configuration[key] = value;
        }

        return configuration;
    }

    private static ScriptedCodexTransport CreateTransportForStoredThread()
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(
                    CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""),
                    cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                await fake.EnqueueServerMessageAsync(
                    CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""),
                    cancellationToken);

                var turnCompleted = JsonSerializer.Serialize(new
                {
                    method = "turn/completed",
                    @params = new
                    {
                        threadId = message.GetProperty("params").GetProperty("threadId").GetString() ?? "thread-from-store",
                        turn = new
                        {
                            id = "turn-1",
                            status = "completed",
                            items = new object[]
                            {
                                new
                                {
                                    type = "agentMessage",
                                    text = "ok",
                                },
                            },
                        },
                    },
                });
                await fake.EnqueueServerMessageAsync(turnCompleted, cancellationToken);
                fake.CompleteServerMessages();
            }
        };

        return transport;
    }

    private static bool IsRequest(JsonElement message, string methodName)
    {
        return message.TryGetProperty("method", out var method)
            && string.Equals(method.GetString(), methodName, StringComparison.Ordinal)
            && message.TryGetProperty("id", out _);
    }

    private static string GetId(JsonElement message)
    {
        var idElement = message.GetProperty("id");
        return idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateResponse(string id, string rawResultJson)
        => $$"""{"id":{{id}},"result":{{rawResultJson}}}""";

    private static bool HasRequest(ScriptedCodexTransport transport, string methodName)
    {
        return transport.SentLines.Any(line =>
        {
            using var document = JsonDocument.Parse(line);
            return IsRequest(document.RootElement, methodName);
        });
    }

    private sealed class TrackingThreadStore(CodexThreadRecord seededRecord) : ICodexThreadStore
    {
        private CodexThreadRecord _record = seededRecord;

        public int TryGetByKeyCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public Task<CodexThreadRecord?> TryGetByKeyAsync(string threadKey, string? threadStorePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryGetByKeyCallCount++;
            return Task.FromResult<CodexThreadRecord?>(string.Equals(_record.ThreadKey, threadKey, StringComparison.Ordinal) ? _record : null);
        }

        public Task<IReadOnlyList<CodexThreadRecord>> ListAsync(string? threadStorePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<CodexThreadRecord>>([_record]);
        }

        public Task SaveAsync(CodexThreadRecord record, string? threadStorePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            _record = record;
            return Task.CompletedTask;
        }
    }
}
