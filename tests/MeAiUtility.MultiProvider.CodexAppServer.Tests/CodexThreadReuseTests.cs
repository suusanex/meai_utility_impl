using System.Text.Json;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.CodexAppServer.Tests.Fakes;
using MeAiUtility.MultiProvider.CodexAppServer.Threading;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests;

public class CodexThreadReuseTests
{
    [Test]
    public async Task GetResponseAsync_AlwaysNew_SendsThreadStartPerCall()
    {
        var firstTransport = CreateBasicTransport("thread-a");
        var firstSut = CreateSut(firstTransport);
        _ = await firstSut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
        Assert.That(CountRequests(firstTransport, "thread/start"), Is.EqualTo(1));

        var secondTransport = CreateBasicTransport("thread-b");
        var secondSut = CreateSut(secondTransport);
        _ = await secondSut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
        Assert.That(CountRequests(secondTransport, "thread/start"), Is.EqualTo(1));
    }

    [Test]
    public async Task GetResponseAsync_ReuseByThreadId_DoesNotSendThreadStart()
    {
        var transport = CreateBasicTransport();
        var options = new CodexAppServerProviderOptions
        {
            ThreadReusePolicy = CodexThreadReusePolicy.ReuseByThreadId,
            ThreadId = "thread-reuse-1",
        };

        var sut = CreateSut(transport, options);
        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(HasRequest(transport, "thread/start"), Is.False);
        using var turnStart = ParseSentRequest(transport, "turn/start");
        Assert.That(turnStart.RootElement.GetProperty("params").GetProperty("threadId").GetString(), Is.EqualTo("thread-reuse-1"));
    }

    [Test]
    public void GetResponseAsync_ReuseByThreadId_ThrowsWhenThreadIdMissing()
    {
        var transport = new ScriptedCodexTransport();
        var options = new CodexAppServerProviderOptions
        {
            ThreadReusePolicy = CodexThreadReusePolicy.ReuseByThreadId,
        };

        var sut = CreateSut(transport, options);
        var ex = Assert.ThrowsAsync<InvalidRequestException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("ThreadId"));
    }

    [Test]
    public async Task GetResponseAsync_ReuseOrCreateByKey_UsesStoredRecord()
    {
        var store = new StubCodexThreadStore();
        var staleLastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await store.SaveAsync(
            new CodexThreadRecord("meai:key:1", "thread-stored", "stored", @"D:\repo", "gpt-5.5-codex", DateTimeOffset.UtcNow.AddHours(-1), staleLastUsedAt),
            null,
            CancellationToken.None);

        var transport = CreateBasicTransport();
        var options = new CodexAppServerProviderOptions
        {
            ThreadReusePolicy = CodexThreadReusePolicy.ReuseOrCreateByKey,
            ThreadKey = "meai:key:1",
        };

        var sut = CreateSut(transport, options, store);
        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(HasRequest(transport, "thread/start"), Is.False);
        using var turnStart = ParseSentRequest(transport, "turn/start");
        Assert.That(turnStart.RootElement.GetProperty("params").GetProperty("threadId").GetString(), Is.EqualTo("thread-stored"));

        var saved = await store.TryGetByKeyAsync("meai:key:1", null, CancellationToken.None);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.LastUsedAt, Is.GreaterThan(staleLastUsedAt));
    }

    [Test]
    public async Task GetResponseAsync_ReuseOrCreateByKey_CreatesAndSavesWhenMissing()
    {
        var store = new StubCodexThreadStore();
        var transport = CreateBasicTransport("thread-created");
        var options = new CodexAppServerProviderOptions
        {
            ThreadReusePolicy = CodexThreadReusePolicy.ReuseOrCreateByKey,
            ThreadKey = "meai:key:new",
            ThreadName = "new thread",
        };

        var sut = CreateSut(transport, options, store);
        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.That(HasRequest(transport, "thread/start"), Is.True);
        using var turnStart = ParseSentRequest(transport, "turn/start");
        Assert.That(turnStart.RootElement.GetProperty("params").GetProperty("threadId").GetString(), Is.EqualTo("thread-created"));

        var saved = await store.TryGetByKeyAsync("meai:key:new", null, CancellationToken.None);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.ThreadId, Is.EqualTo("thread-created"));
        Assert.That(saved.ThreadName, Is.EqualTo("new thread"));
    }

    [Test]
    public void GetResponseAsync_ReuseOrCreateByKey_ThrowsWhenThreadKeyMissing()
    {
        var transport = new ScriptedCodexTransport();
        var options = new CodexAppServerProviderOptions
        {
            ThreadReusePolicy = CodexThreadReusePolicy.ReuseOrCreateByKey,
        };

        var sut = CreateSut(transport, options);
        var ex = Assert.ThrowsAsync<InvalidRequestException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]));
        Assert.That(ex!.Message, Does.Contain("ThreadKey"));
    }

    [Test]
    public async Task GetResponseAsync_Extensions_MapThreadReuseOptionsToRuntime()
    {
        var store = new StubCodexThreadStore();
        var transport = CreateBasicTransport("thread-ext-created");
        var sut = CreateSut(transport, threadStore: store);

        var options = CreateOptionsWithExtensions(
            ("codex.threadReusePolicy", "reuseOrCreateByKey"),
            ("codex.threadKey", "ext-key"),
            ("codex.threadName", "ext-name"),
            ("codex.threadStorePath", @"D:\tmp\codex-threads-ext.json"),
            ("codex.threadId", "ignored-for-policy"));

        _ = await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], options);

        Assert.That(store.LastPathUsed, Is.EqualTo(@"D:\tmp\codex-threads-ext.json"));
        var saved = await store.TryGetByKeyAsync("ext-key", @"D:\tmp\codex-threads-ext.json", CancellationToken.None);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.ThreadName, Is.EqualTo("ext-name"));
        Assert.That(saved.ThreadId, Is.EqualTo("thread-ext-created"));
    }

    [TestCase("codex.threadReusePolicy", true)]
    [TestCase("codex.threadId", 42)]
    [TestCase("codex.threadKey", false)]
    [TestCase("codex.threadName", 10)]
    [TestCase("codex.threadStorePath", 10L)]
    public void GetResponseAsync_Extensions_ThrowWhenTypeMismatch(string key, object value)
    {
        var transport = new ScriptedCodexTransport();
        var sut = CreateSut(transport);
        var options = CreateOptionsWithExtensions((key, value));

        var ex = Assert.ThrowsAsync<InvalidRequestException>(async () => await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], options));
        Assert.That(ex!.Message, Does.Contain(key));
    }

    private static ScriptedCodexTransport CreateBasicTransport(string threadId = "thread-1")
    {
        var transport = new ScriptedCodexTransport();
        transport.OnClientMessageAsync = async (message, fake, cancellationToken) =>
        {
            if (IsRequest(message, "initialize"))
            {
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"codexHome":"C:\\Users\\test","platformFamily":"windows","platformOs":"windows","userAgent":"codex-test"}"""), cancellationToken);
                return;
            }

            if (IsRequest(message, "thread/start"))
            {
                var threadStartResult = JsonSerializer.Serialize(new
                {
                    thread = new
                    {
                        id = threadId,
                    },
                });
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), threadStartResult), cancellationToken);
                return;
            }

            if (IsRequest(message, "turn/start"))
            {
                var turnThreadId = message.GetProperty("params").GetProperty("threadId").GetString() ?? threadId;
                await fake.EnqueueServerMessageAsync(CreateResponse(GetId(message), """{"turn":{"id":"turn-1"}}"""), cancellationToken);
                var turnCompleted = JsonSerializer.Serialize(new
                {
                    method = "turn/completed",
                    @params = new
                    {
                        threadId = turnThreadId,
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

    private static CodexAppServerChatClient CreateSut(
        ScriptedCodexTransport transport,
        CodexAppServerProviderOptions? options = null,
        ICodexThreadStore? threadStore = null)
    {
        return new CodexAppServerChatClient(
            options ?? new CodexAppServerProviderOptions(),
            new StubCodexTransportFactory(transport),
            threadStore ?? new StubCodexThreadStore(),
            new NullLogger<CodexAppServerChatClient>(),
            NullLoggerFactory.Instance);
    }

    private static ChatOptions CreateOptionsWithExtensions(params (string Key, object? Value)[] values)
    {
        var ext = new ExtensionParameters();
        foreach (var (key, value) in values)
        {
            ext.Set(key, value);
        }

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        return options;
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

    private static int CountRequests(ScriptedCodexTransport transport, string methodName)
    {
        return transport.SentLines.Count(line =>
        {
            using var document = JsonDocument.Parse(line);
            return IsRequest(document.RootElement, methodName);
        });
    }

    private static JsonDocument ParseSentRequest(ScriptedCodexTransport transport, string methodName)
    {
        var line = transport.SentLines.First(line =>
        {
            using var document = JsonDocument.Parse(line);
            return IsRequest(document.RootElement, methodName);
        });

        return JsonDocument.Parse(line);
    }
}
